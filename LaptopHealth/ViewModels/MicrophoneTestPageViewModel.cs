using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels.Infrastructure;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace LaptopHealth.ViewModels
{
    /// <summary>
    /// ViewModel for MicrophoneTestPage - handles microphone operations and visualization
    /// </summary>
    public class MicrophoneTestPageViewModel : ViewModelBase, IDisposable
    {
        #region Fields & Constants

        private readonly IAudioService _audioService;
        private CancellationTokenSource? _frequencyCapureTokenSource;
        private Task? _frequencyRenderTask;
        private readonly SemaphoreSlim _uiOperationLock = new(1, 1);
        private CancellationTokenSource? _currentOperationCts;
        private bool _isLoadingDevices;
        private bool _isCleanedUp;
        private readonly TaskCompletionSource<bool> _devicesLoadedTcs = new();

        private const int FREQUENCY_UPDATE_DELAY_MS = 50;
        private const int STOP_TIMEOUT_MS = 2000;
        private const int ERROR_RETRY_DELAY_MS = 100;
        private const string START_MICROPHONE_TEXT = "Start Microphone";
        private const int FREQUENCY_BANDS = 32;

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

        private string _startStopButtonContent = START_MICROPHONE_TEXT;
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

        private string _microphoneStatusText = "Microphone Preview";
        public string MicrophoneStatusText
        {
            get => _microphoneStatusText;
            set => SetProperty(ref _microphoneStatusText, value);
        }

        private string _lastActionText = "None";
        public string LastActionText
        {
            get => _lastActionText;
            set => SetProperty(ref _lastActionText, value);
        }

        // Frequency band data (32 bands)
        private ObservableCollection<FrequencyBand> _frequencyBands = [];
        public ObservableCollection<FrequencyBand> FrequencyBands
        {
            get => _frequencyBands;
            set => SetProperty(ref _frequencyBands, value);
        }

        #endregion

        #region Commands

        public ICommand StartStopMicrophoneCommand { get; }
        public ICommand RefreshDevicesCommand { get; }

        #endregion

        #region Constructor

        public MicrophoneTestPageViewModel(IAudioService audioService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));

            // Initialize frequency bands
            for (int i = 0; i < FREQUENCY_BANDS; i++)
            {
                FrequencyBands.Add(new FrequencyBand { BandIndex = i, Magnitude = 0 });
            }

            // Initialize commands
            StartStopMicrophoneCommand = new AsyncRelayCommand(
                async _ => await ToggleMicrophoneAsync(),
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

                if (_frequencyRenderTask?.IsCompleted == false)
                {
                    LogDebug("WARNING: Frequency capture still running from previous load - cleaning up");
                    await StopMicrophoneAsync(CancellationToken.None);
                }

                // Wait for devices to load
                await _devicesLoadedTcs.Task;

                if (_isCleanedUp)
                {
                    LogDebug("Page was cleaned up while loading devices, skipping auto-start");
                    return;
                }

                // Auto-start microphone if a device is selected
                if (SelectedDevice != null && !_audioService.IsMicrophoneRunning)
                {
                    await ExecuteAudioOperationAsync(async ct =>
                    {
                        await StartMicrophoneAsync(ct);
                        UpdateMicrophoneStatus();
                        UpdateLastAction();
                    }, "Auto-Start Microphone");
                }
            }
            catch (Exception ex)
            {
                LogError("Auto-start microphone on page load", ex);
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

                // Stop microphone if running
                if (_audioService.IsMicrophoneRunning)
                {
                    LogDebug("Stopping microphone during cleanup");
                    await StopMicrophoneAsync(CancellationToken.None);
                }

                // Stop frequency capture if still running
                if (_frequencyRenderTask?.IsCompleted == false)
                {
                    LogDebug("Cancelling frequency capture during cleanup");
                    if (_frequencyCapureTokenSource != null)
                    {
                        await _frequencyCapureTokenSource.CancelAsync();
                    }

                    // Wait for frequency task to complete
                    if (_frequencyRenderTask != null)
                    {
                        await Task.WhenAny(_frequencyRenderTask, Task.Delay(1000));
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
                var devices = (await _audioService.GetAvailableDevicesAsync()).ToList();

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
                await ExecuteAudioOperationAsync(async ct =>
                {
                    // Stop current microphone if running
                    if (_audioService.IsMicrophoneRunning)
                    {
                        await StopMicrophoneAsync(ct);
                    }

                    // Select new device
                    await _audioService.SelectDeviceAsync(deviceName);

                    LogDebug($"Device switched to: {deviceName}");
                    UpdateMicrophoneStatus();
                    UpdateLastAction();

                }, "Switch Device");
            }
            catch (OperationCanceledException ex)
            {
                LogDebug($"Device selection canceled: {ex.Message}");
            }
        }

        #endregion

        #region Microphone Control

        private async Task ToggleMicrophoneAsync()
        {
            try
            {
                await ExecuteAudioOperationAsync(async ct =>
                {
                    if (_audioService.IsMicrophoneRunning)
                    {
                        await StopMicrophoneAsync(ct);
                    }
                    else
                    {
                        await StartMicrophoneAsync(ct);
                    }

                    UpdateMicrophoneStatus();
                    UpdateLastAction();

                }, "Toggle Microphone");
            }
            catch (OperationCanceledException ex)
            {
                LogDebug($"Toggle microphone canceled: {ex.Message}");
            }
        }

        private async Task StartMicrophoneAsync(CancellationToken ct)
        {
            if (_isCleanedUp)
            {
                LogDebug("Page is being cleaned up, aborting microphone start");
                return;
            }

            // Ensure device is selected
            if (_audioService.SelectedDevice == null)
            {
                if (SelectedDevice != null)
                {
                    await _audioService.SelectDeviceAsync(SelectedDevice);
                }
                else
                {
                    MicrophoneStatusText = "No Device Selected";
                    return;
                }
            }

            ct.ThrowIfCancellationRequested();

            var result = await _audioService.StartMicrophoneAsync();

            if (result)
            {
                StartFrequencyCapture();
                UpdateUIForRunningMicrophone();
            }
            else
            {
                MicrophoneStatusText = "Failed to Start";
            }
        }

        private async Task StopMicrophoneAsync(CancellationToken ct)
        {
            // Cancel frequency capture
            if (_frequencyCapureTokenSource != null)
            {
                await _frequencyCapureTokenSource.CancelAsync();
            }

            // Wait for frequency task (with timeout)
            if (_frequencyRenderTask != null && !_frequencyRenderTask.IsCompleted)
            {
                await Task.WhenAny(_frequencyRenderTask, Task.Delay(STOP_TIMEOUT_MS, ct));
            }

            ct.ThrowIfCancellationRequested();

            // Stop hardware
            await _audioService.StopMicrophoneAsync();

            // Clear visualization
            ClearFrequencyVisualization();

            // Cleanup
            _frequencyCapureTokenSource?.Dispose();
            _frequencyCapureTokenSource = null;
            _frequencyRenderTask = null;

            UpdateUIForStoppedMicrophone();
        }

        private async Task RefreshDevicesAsync()
        {
            try
            {
                await ExecuteAudioOperationAsync(async ct =>
                {
                    LastActionText = "Refreshing devices...";

                    // Stop microphone if running
                    if (_audioService.IsMicrophoneRunning)
                    {
                        await StopMicrophoneAsync(ct);
                    }

                    ClearFrequencyVisualization();

                }, "Stop Microphone Before Refresh");

                // Load devices OUTSIDE the cancellable operation
                await LoadAvailableDevicesAsync();

                // Auto-select and initialize first device (but don't start)
                if (AvailableDevices.Count > 0 && SelectedDevice != null)
                {
                    await _audioService.SelectDeviceAsync(SelectedDevice);
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

        #region Frequency Capture

        private void StartFrequencyCapture()
        {
            if (IsFrequencyCaptureRunning())
            {
                return;
            }

            InitializeNewFrequencyCapture();
        }

        private bool IsFrequencyCaptureRunning()
        {
            return _frequencyRenderTask?.IsCompleted == false;
        }

        private void InitializeNewFrequencyCapture()
        {
            _frequencyCapureTokenSource?.Dispose();
            _frequencyCapureTokenSource = new CancellationTokenSource();
            _frequencyRenderTask = CaptureFrequencyLoopAsync(_frequencyCapureTokenSource.Token);
        }

        private async Task CaptureFrequencyLoopAsync(CancellationToken cancellationToken)
        {
            LogDebug("Frequency capture loop STARTED");

            try
            {
                while (ShouldContinueCapture(cancellationToken))
                {
                    await ProcessSingleFrequencyUpdateAsync(cancellationToken);
                }
            }
            finally
            {
                LogDebug("Frequency capture loop COMPLETED");
            }
        }

        private bool ShouldContinueCapture(CancellationToken cancellationToken)
        {
            return !cancellationToken.IsCancellationRequested
                   && _audioService.IsMicrophoneRunning
                   && !_isCleanedUp;
        }

        private async Task ProcessSingleFrequencyUpdateAsync(CancellationToken cancellationToken)
        {
            try
            {
                var frequencyData = await _audioService.GetFrequencyDataAsync();

                if (frequencyData != null && frequencyData.Length == FREQUENCY_BANDS)
                {
                    await UpdateFrequencyBandsAsync(frequencyData, cancellationToken);
                }

                await Task.Delay(FREQUENCY_UPDATE_DELAY_MS, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogError("Frequency capture", ex);
                await Task.Delay(ERROR_RETRY_DELAY_MS, cancellationToken);
            }
        }

        private async Task UpdateFrequencyBandsAsync(float[] frequencyData, CancellationToken cancellationToken)
        {
            // Update UI on main thread
            await Task.Run(() =>
            {
                for (int i = 0; i < frequencyData.Length && i < FrequencyBands.Count; i++)
                {
                    FrequencyBands[i].Magnitude = frequencyData[i];
                }
            }, cancellationToken);
        }

        #endregion

        #region UI Update Methods

        private void ShowNoDevicesState()
        {
            IsDeviceComboBoxEnabled = false;
            SelectedDevice = null;
            IsNoDevicesMessageVisible = true;
            MicrophoneStatusText = "No Devices Available";
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

        private void UpdateUIForRunningMicrophone()
        {
            StartStopButtonContent = "Stop Microphone";
            MicrophoneStatusText = "Microphone Running";
        }

        private void UpdateUIForStoppedMicrophone()
        {
            StartStopButtonContent = START_MICROPHONE_TEXT;
            MicrophoneStatusText = "Microphone Stopped";
        }

        private void ClearFrequencyVisualization()
        {
            foreach (var band in FrequencyBands)
            {
                band.Magnitude = 0;
            }
        }

        private void UpdateLastAction()
        {
            LastActionText = _audioService.LastAction;
        }

        private void UpdateMicrophoneStatus()
        {
            if (_audioService.SelectedDevice != null)
            {
                if (_audioService.IsMicrophoneRunning)
                {
                    UpdateUIForRunningMicrophone();
                }
                else
                {
                    MicrophoneStatusText = "Microphone Ready";
                    StartStopButtonContent = START_MICROPHONE_TEXT;
                }
            }
            else
            {
                MicrophoneStatusText = "No Device Selected";
                StartStopButtonContent = START_MICROPHONE_TEXT;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Ensures only one audio operation executes at a time.
        /// Automatically cancels previous operation if new one is requested.
        /// </summary>
        private async Task<T> ExecuteAudioOperationAsync<T>(
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
            if (_currentOperationCts != null)
            {
                await _currentOperationCts.CancelAsync();
            }

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
        private async Task ExecuteAudioOperationAsync(
            Func<CancellationToken, Task> operation,
            string operationName)
        {
            await ExecuteAudioOperationAsync(async ct =>
            {
                await operation(ct);
                return true;
            }, operationName);
        }

        private static void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[MicrophoneTestPageViewModel] {message}");
        }

        private static void LogError(string operation, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MicrophoneTestPageViewModel] Error in {operation}: {ex.Message}");
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
                _frequencyCapureTokenSource?.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MicrophoneTestPageViewModel()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// Represents a single frequency band for visualization
    /// </summary>
    public class FrequencyBand : ViewModelBase
    {
        private int _bandIndex;
        public int BandIndex
        {
            get => _bandIndex;
            set => SetProperty(ref _bandIndex, value);
        }

        private float _magnitude;
        public float Magnitude
        {
            get => _magnitude;
            set => SetProperty(ref _magnitude, value);
        }
    }
}
