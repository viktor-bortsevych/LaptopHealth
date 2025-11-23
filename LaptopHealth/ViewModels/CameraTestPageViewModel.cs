using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels.Infrastructure;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace LaptopHealth.ViewModels
{
    /// <summary>
    /// ViewModel for CameraTestPage - handles camera operations and state management
    /// </summary>
    public class CameraTestPageViewModel : ViewModelBase, IDisposable
    {
        #region Fields & Constants

        private readonly ICameraService _cameraService;
        private CancellationTokenSource? _frameCaptureTokenSource;
        private Task? _frameRenderTask;
        private readonly SemaphoreSlim _uiOperationLock = new(1, 1);
        private CancellationTokenSource? _currentOperationCts;
        private bool _isLoadingDevices;
        private bool _isCleanedUp;
        private readonly TaskCompletionSource<bool> _devicesLoadedTcs = new();

        private const int FRAME_DELAY_MS = 33;
        private const int STOP_TIMEOUT_MS = 2000;
        private const int ERROR_RETRY_DELAY_MS = 100;
        private const string START_CAMERA_TEXT = "Start Camera";

        #endregion

        #region Properties

        private ObservableCollection<string> _availableDevices = [];
        public ObservableCollection<string> AvailableDevices
        {
            get => _availableDevices;
            set => SetProperty(ref _availableDevices, value);
        }

        private string? _selectedDevice;
        public string? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value) && !_isLoadingDevices && value != null)
                {
                    _ = HandleDeviceSelectionChangedAsync(value);
                }
            }
        }

        private bool _isDeviceComboBoxEnabled;
        public bool IsDeviceComboBoxEnabled
        {
            get => _isDeviceComboBoxEnabled;
            set => SetProperty(ref _isDeviceComboBoxEnabled, value);
        }

        private bool _isNoDevicesMessageVisible = true;
        public bool IsNoDevicesMessageVisible
        {
            get => _isNoDevicesMessageVisible;
            set => SetProperty(ref _isNoDevicesMessageVisible, value);
        }

        private string _startStopButtonContent = "Start Camera";
        public string StartStopButtonContent
        {
            get => _startStopButtonContent;
            set => SetProperty(ref _startStopButtonContent, value);
        }

        private bool _isStartStopButtonEnabled = true;
        public bool IsStartStopButtonEnabled
        {
            get => _isStartStopButtonEnabled;
            set => SetProperty(ref _isStartStopButtonEnabled, value);
        }

        private bool _isRefreshButtonEnabled = true;
        public bool IsRefreshButtonEnabled
        {
            get => _isRefreshButtonEnabled;
            set => SetProperty(ref _isRefreshButtonEnabled, value);
        }

        private string _cameraStatusText = "Camera Preview";
        public string CameraStatusText
        {
            get => _cameraStatusText;
            set => SetProperty(ref _cameraStatusText, value);
        }

        private string _lastActionText = "None";
        public string LastActionText
        {
            get => _lastActionText;
            set => SetProperty(ref _lastActionText, value);
        }

        private bool _isCameraPlaceholderVisible = true;
        public bool IsCameraPlaceholderVisible
        {
            get => _isCameraPlaceholderVisible;
            set => SetProperty(ref _isCameraPlaceholderVisible, value);
        }

        private bool _isCameraPreviewVisible;
        public bool IsCameraPreviewVisible
        {
            get => _isCameraPreviewVisible;
            set => SetProperty(ref _isCameraPreviewVisible, value);
        }

        private BitmapImage? _cameraPreviewSource;
        public BitmapImage? CameraPreviewSource
        {
            get => _cameraPreviewSource;
            set => SetProperty(ref _cameraPreviewSource, value);
        }

        #endregion

        #region Commands

        public ICommand StartStopCameraCommand { get; }
        public ICommand RefreshDevicesCommand { get; }

        #endregion

        #region Constructor

        public CameraTestPageViewModel(ICameraService cameraService)
        {
            _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));

            // Initialize commands
            StartStopCameraCommand = new AsyncRelayCommand(
                async _ => await ToggleCameraAsync(),
                _ => !_isCleanedUp
            );

            RefreshDevicesCommand = new AsyncRelayCommand(
                async _ => await RefreshDevicesAsync(),
                _ => !_isCleanedUp
            );

            // Load devices
            _ = LoadAvailableDevicesAsync();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the ViewModel after the view is loaded
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                LogDebug("InitializeAsync started");

                if (_isCleanedUp)
                {
                    LogDebug("WARNING: Page was already cleaned up, skipping auto-start");
                    return;
                }

                if (_frameRenderTask?.IsCompleted == false)
                {
                    LogDebug("WARNING: Frame capture still running from previous load - cleaning up");
                    await StopCameraAsync(CancellationToken.None);
                }

                // Wait for devices to load
                await _devicesLoadedTcs.Task;

                if (_isCleanedUp)
                {
                    LogDebug("Page was cleaned up while loading devices, skipping auto-start");
                    return;
                }

                // Auto-start camera if a device is selected
                if (SelectedDevice != null && !_cameraService.IsCameraRunning)
                {
                    await ExecuteCameraOperationAsync(async ct =>
                    {
                        await StartCameraAsync(ct);
                        UpdateCameraStatus();
                        UpdateLastAction();
                    }, "Auto-Start Camera");
                }
            }
            catch (Exception ex)
            {
                LogError("Auto-start camera on page load", ex);
            }
        }

        /// <summary>
        /// Performs cleanup before disposal
        /// </summary>
        public async Task CleanupAsync()
        {
            if (_isCleanedUp)
            {
                LogDebug("CleanupAsync already called, skipping");
                return;
            }

            _isCleanedUp = true;
            LogDebug("CleanupAsync starting");

            try
            {
                // Cancel any ongoing operations
                _currentOperationCts?.CancelAsync();

                // Stop camera if running
                if (_cameraService.IsCameraRunning)
                {
                    LogDebug("Stopping camera during cleanup");
                    await StopCameraAsync(CancellationToken.None);
                }

                // Stop frame capture if still running
                if (_frameRenderTask?.IsCompleted == false)
                {
                    LogDebug("Cancelling frame capture during cleanup");
                    if (_frameCaptureTokenSource != null)
                    {
                        await _frameCaptureTokenSource.CancelAsync();
                    }

                    // Wait for frame task to complete
                    if (_frameRenderTask != null)
                    {
                        await Task.WhenAny(_frameRenderTask, Task.Delay(1000));
                    }
                }

                LogDebug("CleanupAsync completed successfully");
            }
            catch (Exception ex)
            {
                LogError("CleanupAsync", ex);
            }
        }

        #endregion

        #region Device Management

        private async Task LoadAvailableDevicesAsync()
        {
            _isLoadingDevices = true;

            try
            {
                var devices = (await _cameraService.GetAvailableDevicesAsync()).ToList();

                AvailableDevices.Clear();

                if (devices.Count == 0)
                {
                    ShowNoDevicesState();
                }
                else
                {
                    ShowDevicesAvailableState(devices);
                    SelectedDevice = devices[0];
                }

                UpdateLastAction();
            }
            finally
            {
                _isLoadingDevices = false;
                _devicesLoadedTcs.TrySetResult(true);
            }
        }

        private async Task HandleDeviceSelectionChangedAsync(string deviceName)
        {
            try
            {
                await ExecuteCameraOperationAsync(async ct =>
                {
                    // Stop current camera if running
                    if (_cameraService.IsCameraRunning)
                    {
                        await StopCameraAsync(ct);
                    }

                    // Select new device
                    await _cameraService.SelectDeviceAsync(deviceName);

                    LogDebug($"Device switched to: {deviceName}");
                    UpdateCameraStatus();
                    UpdateLastAction();

                }, "Switch Device");
            }
            catch (OperationCanceledException ex)
            {
                LogDebug($"Device selection canceled: {ex.Message}");
            }
        }

        #endregion

        #region Camera Control

        private async Task ToggleCameraAsync()
        {
            try
            {
                await ExecuteCameraOperationAsync(async ct =>
                {
                    if (_cameraService.IsCameraRunning)
                    {
                        await StopCameraAsync(ct);
                    }
                    else
                    {
                        await StartCameraAsync(ct);
                    }

                    UpdateCameraStatus();
                    UpdateLastAction();

                }, "Toggle Camera");
            }
            catch (OperationCanceledException ex)
            {
                LogDebug($"Toggle camera canceled: {ex.Message}");
            }
        }

        private async Task StartCameraAsync(CancellationToken ct)
        {
            if (_isCleanedUp)
            {
                LogDebug("Page is being cleaned up, aborting camera start");
                return;
            }

            // Ensure device is selected
            if (_cameraService.SelectedDevice == null)
            {
                if (SelectedDevice != null)
                {
                    await _cameraService.SelectDeviceAsync(SelectedDevice);
                }
                else
                {
                    CameraStatusText = "No Device Selected";
                    return;
                }
            }

            ct.ThrowIfCancellationRequested();

            var result = await _cameraService.StartCameraAsync();

            if (result)
            {
                StartFrameCapture();
                UpdateUIForRunningCamera();
            }
            else
            {
                CameraStatusText = "Failed to Start";
            }
        }

        private async Task StopCameraAsync(CancellationToken ct)
        {
            // Cancel frame capture
            if (_frameCaptureTokenSource != null)
            {
                await _frameCaptureTokenSource.CancelAsync();
            }

            // Wait for frame task (with timeout)
            if (_frameRenderTask != null && !_frameRenderTask.IsCompleted)
            {
                await Task.WhenAny(_frameRenderTask, Task.Delay(STOP_TIMEOUT_MS, ct));
            }

            ct.ThrowIfCancellationRequested();

            // Stop hardware
            await _cameraService.StopCameraAsync();

            // Update UI
            ClearCameraPreview();

            // Cleanup
            _frameCaptureTokenSource?.Dispose();
            _frameCaptureTokenSource = null;
            _frameRenderTask = null;

            UpdateUIForStoppedCamera();
        }

        private async Task RefreshDevicesAsync()
        {
            try
            {
                await ExecuteCameraOperationAsync(async ct =>
                {
                    LastActionText = "Refreshing devices...";

                    // Stop camera if running
                    if (_cameraService.IsCameraRunning)
                    {
                        await StopCameraAsync(ct);
                    }

                    ClearCameraPreview();

                }, "Stop Camera Before Refresh");

                // Load devices OUTSIDE the cancellable operation
                await LoadAvailableDevicesAsync();

                // Auto-select and initialize first device (but don't start)
                if (AvailableDevices.Count > 0 && SelectedDevice != null)
                {
                    await _cameraService.SelectDeviceAsync(SelectedDevice);
                    LogDebug($"Device re-initialized after refresh: {SelectedDevice}");
                }

                LastActionText = "Devices refreshed";
            }
            catch (OperationCanceledException ex)
            {
                LogDebug($"Refresh devices canceled: {ex.Message}");
            }
        }

        #endregion

        #region Frame Capture

        private void StartFrameCapture()
        {
            if (IsFrameCaptureRunning())
            {
                return;
            }

            InitializeNewFrameCapture();
        }

        private bool IsFrameCaptureRunning()
        {
            return _frameRenderTask?.IsCompleted == false;
        }

        private void InitializeNewFrameCapture()
        {
            _frameCaptureTokenSource?.Dispose();
            _frameCaptureTokenSource = new CancellationTokenSource();
            _frameRenderTask = CaptureFrameLoopAsync(_frameCaptureTokenSource.Token);
        }

        private async Task CaptureFrameLoopAsync(CancellationToken cancellationToken)
        {
            LogDebug("Frame capture loop STARTED");

            try
            {
                while (ShouldContinueCapture(cancellationToken))
                {
                    await ProcessSingleFrameAsync(cancellationToken);
                }
            }
            finally
            {
                LogDebug("Frame capture loop COMPLETED");
            }
        }

        private bool ShouldContinueCapture(CancellationToken cancellationToken)
        {
            return !cancellationToken.IsCancellationRequested
                   && _cameraService.IsCameraRunning
                   && !_isCleanedUp;
        }

        private async Task ProcessSingleFrameAsync(CancellationToken cancellationToken)
        {
            try
            {
                var frameBytes = await _cameraService.GetCurrentFrameAsync();

                if (frameBytes != null && frameBytes.Length > 0)
                {
                    await RenderFrameAsync(frameBytes, cancellationToken);
                }

                await Task.Delay(FRAME_DELAY_MS, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogError("Frame capture", ex);
                await Task.Delay(ERROR_RETRY_DELAY_MS, cancellationToken);
            }
        }

        private async Task RenderFrameAsync(byte[] frameBytes, CancellationToken cancellationToken)
        {
            var bitmap = await Task.Run(() => CreateBitmapFromBytes(frameBytes), cancellationToken);

            if (bitmap != null && ShouldContinueCapture(cancellationToken))
            {
                ShowCameraPreview(bitmap);
            }
        }

        private static BitmapImage? CreateBitmapFromBytes(byte[] frameBytes)
        {
            try
            {
                var bitmap = new BitmapImage();

                using (var memoryStream = new System.IO.MemoryStream(frameBytes))
                {
                    bitmap.BeginInit();
                    bitmap.StreamSource = memoryStream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                }

                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                LogError("Create bitmap", ex);
                return null;
            }
        }

        private void ShowCameraPreview(BitmapImage bitmap)
        {
            IsCameraPlaceholderVisible = false;
            CameraPreviewSource = bitmap;
            IsCameraPreviewVisible = true;
        }

        #endregion

        #region UI Update Methods

        private void ShowNoDevicesState()
        {
            IsDeviceComboBoxEnabled = false;
            SelectedDevice = null;
            IsNoDevicesMessageVisible = true;
            CameraStatusText = "No Devices Available";
        }

        private void ShowDevicesAvailableState(List<string> devices)
        {
            IsNoDevicesMessageVisible = false;
            IsDeviceComboBoxEnabled = true;

            foreach (var device in devices)
            {
                AvailableDevices.Add(device);
            }

            LogDebug($"Found {devices.Count} device(s)");
        }

        private void UpdateUIForRunningCamera()
        {
            StartStopButtonContent = "Stop Camera";
            CameraStatusText = "Camera Running";
        }

        private void UpdateUIForStoppedCamera()
        {
            StartStopButtonContent = START_CAMERA_TEXT;
            CameraStatusText = "Camera Stopped";
        }

        private void ClearCameraPreview()
        {
            IsCameraPlaceholderVisible = true;
            CameraPreviewSource = null;
            IsCameraPreviewVisible = false;
        }

        private void UpdateLastAction()
        {
            LastActionText = _cameraService.LastAction;
        }

        private void UpdateCameraStatus()
        {
            if (_cameraService.SelectedDevice != null)
            {
                if (_cameraService.IsCameraRunning)
                {
                    UpdateUIForRunningCamera();
                }
                else
                {
                    CameraStatusText = "Camera Ready";
                    StartStopButtonContent = START_CAMERA_TEXT;
                }
            }
            else
            {
                CameraStatusText = "No Device Selected";
                StartStopButtonContent = START_CAMERA_TEXT;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Ensures only one camera operation executes at a time.
        /// Automatically cancels previous operation if new one is requested.
        /// </summary>
        private async Task<T> ExecuteCameraOperationAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName)
        {
            // Check if page is being cleaned up
            if (_isCleanedUp)
            {
                LogDebug($"Page is being cleaned up, skipping operation: {operationName}");
                return default!;
            }

            // Disable UI
            IsStartStopButtonEnabled = false;
            IsDeviceComboBoxEnabled = false;
            IsRefreshButtonEnabled = false;

            // Cancel any in-progress operation
            _currentOperationCts?.CancelAsync();

            // Wait for previous operation to finish
            await _uiOperationLock.WaitAsync();

            try
            {
                _currentOperationCts?.Dispose();
                _currentOperationCts = new CancellationTokenSource();

                LogDebug($"Starting: {operationName}");

                var result = await operation(_currentOperationCts.Token);

                LogDebug($"Completed: {operationName}");
                return result;
            }
            catch (OperationCanceledException)
            {
                LogDebug($"Cancelled: {operationName}");
                throw;
            }
            catch (Exception ex)
            {
                LogError(operationName, ex);
                throw;
            }
            finally
            {
                _uiOperationLock.Release();

                // Re-enable UI (only if not cleaned up)
                if (!_isCleanedUp)
                {
                    IsStartStopButtonEnabled = true;
                    IsDeviceComboBoxEnabled = AvailableDevices.Count > 0;
                    IsRefreshButtonEnabled = true;
                }
            }
        }

        // Overload for void operations
        private async Task ExecuteCameraOperationAsync(
            Func<CancellationToken, Task> operation,
            string operationName)
        {
            await ExecuteCameraOperationAsync(async ct =>
            {
                await operation(ct);
                return true;
            }, operationName);
        }

        private static void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[CameraTestPageViewModel] {message}");
        }

        private static void LogError(string operation, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CameraTestPageViewModel] Error in {operation}: {ex.Message}");
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose managed resources
                _uiOperationLock.Dispose();
                _currentOperationCts?.Dispose();
                _frameCaptureTokenSource?.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~CameraTestPageViewModel()
        {
            Dispose(false);
        }

        #endregion
    }
}
