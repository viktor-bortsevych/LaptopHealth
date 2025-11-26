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
                LogInfo("[CameraTestPageViewModel] InitializeAsync started");
                LogInfo($"[CameraTestPageViewModel] Instance hash: {GetHashCode()}");

                if (_isCleanedUp)
                {
                    LogWarn("[CameraTestPageViewModel] WARNING: Page was already cleaned up, skipping auto-start");
                    return;
                }

                if (_frameRenderTask?.IsCompleted == false)
                {
                    LogWarn("[CameraTestPageViewModel] WARNING: Frame capture still running from previous load - cleaning up");
                    await StopCameraAsync(CancellationToken.None);
                }

                // Wait for devices to load
                LogDebug("[CameraTestPageViewModel] Waiting for devices to load");
                await _devicesLoadedTcs.Task;

                if (_isCleanedUp)
                {
                    LogWarn("[CameraTestPageViewModel] Page was cleaned up while loading devices, skipping auto-start");
                    return;
                }

                // Auto-start camera if a device is selected
                if (SelectedDevice != null && !_cameraService.IsCameraRunning)
                {
                    LogInfo("[CameraTestPageViewModel] Auto-starting camera with device: " + SelectedDevice);
                    await ExecuteCameraOperationAsync(async ct =>
                    {
                        await StartCameraAsync(ct);
                        UpdateCameraStatus();
                        UpdateLastAction();
                    }, "Auto-Start Camera");
                }

                LogInfo("[CameraTestPageViewModel] InitializeAsync completed successfully");
            }
            catch (Exception ex)
            {
                LogError("[CameraTestPageViewModel] Auto-start camera on page load", ex);
            }
        }

        /// <summary>
        /// Performs cleanup before disposal
        /// </summary>
        public async Task CleanupAsync()
        {
            if (_isCleanedUp)
            {
                LogWarn("[CameraTestPageViewModel] CleanupAsync already called, skipping");
                return;
            }

            LogInfo("[CameraTestPageViewModel] ===============================================================================");
            LogInfo("[CameraTestPageViewModel] CleanupAsync starting");
            LogInfo($"[CameraTestPageViewModel] Instance hash: {GetHashCode()}");
            LogInfo($"[CameraTestPageViewModel] Is camera running: {_cameraService.IsCameraRunning}");
            LogInfo($"[CameraTestPageViewModel] Frame render task active: {_frameRenderTask?.IsCompleted == false}");

            _isCleanedUp = true;

            try
            {
                // Cancel any ongoing operations
                LogDebug("[CameraTestPageViewModel] Cancelling ongoing operations");
                if (_currentOperationCts != null)
                {
                    await _currentOperationCts.CancelAsync();
                    LogDebug("[CameraTestPageViewModel] Current operation cancelled");
                }

                // Stop camera if running
                if (_cameraService.IsCameraRunning)
                {
                    LogInfo("[CameraTestPageViewModel] Stopping camera during cleanup");
                    await StopCameraAsync(CancellationToken.None);
                    LogInfo("[CameraTestPageViewModel] Camera stopped");
                }

                // Stop frame capture if still running
                if (_frameRenderTask?.IsCompleted == false)
                {
                    LogDebug("[CameraTestPageViewModel] Cancelling frame capture during cleanup");
                    if (_frameCaptureTokenSource != null)
                    {
                        await _frameCaptureTokenSource.CancelAsync();
                    }

                    // Wait for frame task to complete
                    if (_frameRenderTask != null)
                    {
                        LogDebug("[CameraTestPageViewModel] Waiting for frame task to complete (max 1 second)");
                        var completed = await Task.WhenAny(_frameRenderTask, Task.Delay(1000));
                        if (completed == _frameRenderTask)
                        {
                            LogInfo("[CameraTestPageViewModel] Frame task completed");
                        }
                        else
                        {
                            LogWarn("[CameraTestPageViewModel] Frame task did not complete within timeout");
                        }
                    }
                }

                LogInfo("[CameraTestPageViewModel] CleanupAsync completed successfully");
            }
            catch (Exception ex)
            {
                LogError("[CameraTestPageViewModel] CleanupAsync", ex);
            }
            finally
            {
                LogInfo("[CameraTestPageViewModel] ===============================================================================");
            }
        }

        #endregion

        #region Device Management

        private async Task LoadAvailableDevicesAsync()
        {
            LogInfo("[CameraTestPageViewModel] LoadAvailableDevicesAsync starting");
            _isLoadingDevices = true;

            try
            {
                var devices = (await _cameraService.GetAvailableDevicesAsync()).ToList();

                LogInfo($"[CameraTestPageViewModel] Found {devices.Count} camera device(s)");
                for (int i = 0; i < devices.Count; i++)
                {
                    LogDebug($"[CameraTestPageViewModel]   Device {i}: {devices[i]}");
                }

                AvailableDevices.Clear();

                if (devices.Count == 0)
                {
                    LogWarn("[CameraTestPageViewModel] No camera devices found");
                    ShowNoDevicesState();
                }
                else
                {
                    LogInfo($"[CameraTestPageViewModel] Showing {devices.Count} devices in UI");
                    ShowDevicesAvailableState(devices);
                    SelectedDevice = devices[0];
                    LogInfo($"[CameraTestPageViewModel] Auto-selected first device: {devices[0]}");
                }

                UpdateLastAction();
            }
            finally
            {
                _isLoadingDevices = false;
                _devicesLoadedTcs.TrySetResult(true);
                LogDebug("[CameraTestPageViewModel] LoadAvailableDevicesAsync completed");
            }
        }

        private async Task HandleDeviceSelectionChangedAsync(string deviceName)
        {
            LogInfo($"[CameraTestPageViewModel] Device selection changed: {deviceName}");
            try
            {
                await ExecuteCameraOperationAsync(async ct =>
                {
                    // Stop current camera if running
                    if (_cameraService.IsCameraRunning)
                    {
                        LogDebug("[CameraTestPageViewModel] Stopping camera before device switch");
                        await StopCameraAsync(ct);
                    }

                    // Select new device
                    LogDebug($"[CameraTestPageViewModel] Selecting device: {deviceName}");
                    await _cameraService.SelectDeviceAsync(deviceName);

                    LogInfo($"[CameraTestPageViewModel] Device switched successfully to: {deviceName}");
                    UpdateCameraStatus();
                    UpdateLastAction();

                }, "Switch Device");
            }
            catch (OperationCanceledException ex)
            {
                LogDebug($"[CameraTestPageViewModel] Device selection canceled: {ex.Message}");
            }
        }

        #endregion

        #region Camera Control

        private async Task ToggleCameraAsync()
        {
            LogInfo("[CameraTestPageViewModel] Toggle camera command invoked");
            try
            {
                await ExecuteCameraOperationAsync(async ct =>
                {
                    if (_cameraService.IsCameraRunning)
                    {
                        LogInfo("[CameraTestPageViewModel] Camera is running, stopping");
                        await StopCameraAsync(ct);
                    }
                    else
                    {
                        LogInfo("[CameraTestPageViewModel] Camera is stopped, starting");
                        await StartCameraAsync(ct);
                    }

                    UpdateCameraStatus();
                    UpdateLastAction();

                }, "Toggle Camera");
            }
            catch (OperationCanceledException ex)
            {
                LogDebug($"[CameraTestPageViewModel] Toggle camera canceled: {ex.Message}");
            }
        }

        private async Task StartCameraAsync(CancellationToken ct)
        {
            LogInfo("[CameraTestPageViewModel] StartCameraAsync called");

            if (_isCleanedUp)
            {
                LogWarn("[CameraTestPageViewModel] Page is being cleaned up, aborting camera start");
                return;
            }

            // Ensure device is selected
            if (_cameraService.SelectedDevice == null)
            {
                LogDebug("[CameraTestPageViewModel] No device currently selected, selecting from UI");
                if (SelectedDevice != null)
                {
                    await _cameraService.SelectDeviceAsync(SelectedDevice);
                    LogInfo($"[CameraTestPageViewModel] Selected device: {SelectedDevice}");
                }
                else
                {
                    CameraStatusText = "No Device Selected";
                    LogWarn("[CameraTestPageViewModel] No device available to start camera");
                    return;
                }
            }

            ct.ThrowIfCancellationRequested();

            LogDebug("[CameraTestPageViewModel] Calling StartCameraAsync on service");
            var result = await _cameraService.StartCameraAsync();

            if (result)
            {
                LogInfo("[CameraTestPageViewModel] Camera started successfully");
                StartFrameCapture();
                UpdateUIForRunningCamera();
            }
            else
            {
                LogError("[CameraTestPageViewModel] Failed to start camera", null);
                CameraStatusText = "Failed to Start";
            }
        }

        private async Task StopCameraAsync(CancellationToken ct)
        {
            LogInfo("[CameraTestPageViewModel] StopCameraAsync called");

            // Cancel frame capture
            if (_frameCaptureTokenSource != null)
            {
                LogDebug("[CameraTestPageViewModel] Cancelling frame capture token source");
                await _frameCaptureTokenSource.CancelAsync();
            }

            // Wait for frame task (with timeout)
            if (_frameRenderTask != null && !_frameRenderTask.IsCompleted)
            {
                LogDebug("[CameraTestPageViewModel] Waiting for frame render task to complete");
                await Task.WhenAny(_frameRenderTask, Task.Delay(STOP_TIMEOUT_MS, ct));
                if (_frameRenderTask.IsCompleted)
                {
                    LogDebug("[CameraTestPageViewModel] Frame render task completed");
                }
                else
                {
                    LogWarn("[CameraTestPageViewModel] Frame render task did not complete within timeout");
                }
            }

            ct.ThrowIfCancellationRequested();

            // Stop hardware
            LogDebug("[CameraTestPageViewModel] Calling StopCameraAsync on service");
            await _cameraService.StopCameraAsync();
            LogInfo("[CameraTestPageViewModel] Camera service stopped");

            // Update UI
            ClearCameraPreview();

            // Cleanup
            LogDebug("[CameraTestPageViewModel] Disposing frame capture resources");
            _frameCaptureTokenSource?.Dispose();
            _frameCaptureTokenSource = null;
            _frameRenderTask = null;

            UpdateUIForStoppedCamera();
            LogInfo("[CameraTestPageViewModel] StopCameraAsync completed");
        }

        private async Task RefreshDevicesAsync()
        {
            LogInfo("[CameraTestPageViewModel] Refresh devices command invoked");
            try
            {
                await ExecuteCameraOperationAsync(async ct =>
                {
                    LastActionText = "Refreshing devices...";

                    // Stop camera if running
                    if (_cameraService.IsCameraRunning)
                    {
                        LogDebug("[CameraTestPageViewModel] Stopping camera before device refresh");
                        await StopCameraAsync(ct);
                    }

                    ClearCameraPreview();

                }, "Stop Camera Before Refresh");

                // Load devices OUTSIDE the cancellable operation
                LogDebug("[CameraTestPageViewModel] Loading available devices");
                await LoadAvailableDevicesAsync();

                // Auto-select and initialize first device (but don't start)
                if (AvailableDevices.Count > 0 && SelectedDevice != null)
                {
                    LogInfo($"[CameraTestPageViewModel] Re-initializing device after refresh: {SelectedDevice}");
                    await _cameraService.SelectDeviceAsync(SelectedDevice);
                }

                LastActionText = "Devices refreshed";
                LogInfo("[CameraTestPageViewModel] Device refresh completed");
            }
            catch (OperationCanceledException ex)
            {
                LogDebug($"[CameraTestPageViewModel] Refresh devices canceled: {ex.Message}");
            }
        }

        #endregion

        #region Frame Capture

        private void StartFrameCapture()
        {
            LogInfo("[CameraTestPageViewModel] StartFrameCapture called");
            if (IsFrameCaptureRunning())
            {
                LogWarn("[CameraTestPageViewModel] Frame capture already running, skipping");
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
            LogInfo("[CameraTestPageViewModel] Initializing new frame capture");
            _frameCaptureTokenSource?.Dispose();
            _frameCaptureTokenSource = new CancellationTokenSource();
            _frameRenderTask = CaptureFrameLoopAsync(_frameCaptureTokenSource.Token);
            LogDebug("[CameraTestPageViewModel] Frame capture loop task started");
        }

        private async Task CaptureFrameLoopAsync(CancellationToken cancellationToken)
        {
            LogInfo("[CameraTestPageViewModel] Frame capture loop STARTED");
            int frameCount = 0;

            try
            {
                while (ShouldContinueCapture(cancellationToken))
                {
                    await ProcessSingleFrameAsync(cancellationToken);
                    frameCount++;
                }
            }
            catch (OperationCanceledException)
            {
                LogDebug($"[CameraTestPageViewModel] Frame capture loop cancelled after {frameCount} frames");
                throw;
            }
            finally
            {
                LogInfo($"[CameraTestPageViewModel] Frame capture loop COMPLETED after {frameCount} frames");
            }
        }

        private bool ShouldContinueCapture(CancellationToken cancellationToken)
        {
            bool shouldContinue = !cancellationToken.IsCancellationRequested
                   && _cameraService.IsCameraRunning
                   && !_isCleanedUp;
            return shouldContinue;
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
                LogError("[CameraTestPageViewModel] Frame capture", ex);
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
                System.Diagnostics.Debug.WriteLine($"[CameraTestPageViewModel] Error creating bitmap: {ex.Message}");
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
                LogWarn($"[CameraTestPageViewModel] Page is being cleaned up, skipping operation: {operationName}");
                return default!;
            }

            // Disable UI
            IsStartStopButtonEnabled = false;
            IsDeviceComboBoxEnabled = false;
            IsRefreshButtonEnabled = false;

            // Cancel any in-progress operation
            if (_currentOperationCts != null)
            {
                LogDebug($"[CameraTestPageViewModel] Cancelling previous operation before: {operationName}");
                await _currentOperationCts.CancelAsync();
            }

            // Wait for previous operation to finish
            await _uiOperationLock.WaitAsync();

            try
            {
                _currentOperationCts?.Dispose();
                _currentOperationCts = new CancellationTokenSource();

                LogDebug($"[CameraTestPageViewModel] Starting: {operationName}");

                var result = await operation(_currentOperationCts.Token);

                LogDebug($"[CameraTestPageViewModel] Completed: {operationName}");
                return result;
            }
            catch (OperationCanceledException)
            {
                LogDebug($"[CameraTestPageViewModel] Cancelled: {operationName}");
                throw;
            }
            catch (Exception ex)
            {
                LogError($"[CameraTestPageViewModel] {operationName}", ex);
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

        private static void LogInfo(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }

        private static void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }

        private static void LogWarn(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }

        private static void LogError(string operation, Exception? ex)
        {
            string message = $"[CameraTestPageViewModel] Error in {operation}: {ex?.Message}";
            System.Diagnostics.Debug.WriteLine(message);
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            LogInfo("[CameraTestPageViewModel] Dispose(bool) called");
            LogInfo($"[CameraTestPageViewModel] Instance hash: {GetHashCode()}");
            LogInfo($"[CameraTestPageViewModel] Disposing: {disposing}");

            if (disposing)
            {
                // Dispose managed resources
                LogDebug("[CameraTestPageViewModel] Disposing UI operation lock");
                _uiOperationLock.Dispose();

                LogDebug("[CameraTestPageViewModel] Disposing current operation CTS");
                _currentOperationCts?.Dispose();

                LogDebug("[CameraTestPageViewModel] Disposing frame capture token source");
                _frameCaptureTokenSource?.Dispose();

                LogInfo("[CameraTestPageViewModel] All managed resources disposed");
            }

            _disposed = true;
            LogInfo("[CameraTestPageViewModel] Dispose(bool) completed");
        }

        public void Dispose()
        {
            LogInfo("[CameraTestPageViewModel] Dispose() method called");
            Dispose(true);
            GC.SuppressFinalize(this);
            LogInfo("[CameraTestPageViewModel] GC.SuppressFinalize called");
        }

        #endregion
    }
}
