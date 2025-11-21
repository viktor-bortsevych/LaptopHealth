using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LaptopHealth.Services.Interfaces;

namespace LaptopHealth.Views
{
    public partial class CameraTestPage : UserControl, ITestPage
    {
        #region Fields & Constants

        private readonly ICameraService _cameraService;
        private CancellationTokenSource? _frameCaptureTokenSource;
        private Task? _frameRenderTask;
        private bool _isOperationInProgress = false;
        private bool _isLoadingDevices = false; // FIX: Prevent selection during load
        private DateTime _lastOperationTime = DateTime.MinValue;

        private const int OPERATION_COOLDOWN_MS = 500;
        private const int FRAME_DELAY_MS = 33;
        private const int STOP_TIMEOUT_MS = 2000;
        private const int DEVICE_RELEASE_DELAY_MS = 300; // FIX: Time for device to release
        private const int ERROR_RETRY_DELAY_MS = 100;

        public string TestName => "Camera Test";
        public string TestDescription => "Tests camera device enumeration and control";

        #endregion

        #region Initialization

        public CameraTestPage()
        {
            InitializeComponent();

            _cameraService = App.ServiceProvider?.GetService(typeof(ICameraService)) as ICameraService
                ?? throw new InvalidOperationException("ICameraService is not registered");

            Unloaded += CameraTestPage_Unloaded;
            _ = LoadAvailableDevicesAsync();
        }

        private async void CameraTestPage_Unloaded(object sender, RoutedEventArgs e)
        {
            await SafeExecuteAsync("Page Cleanup", async () =>
            {
                if (_cameraService.IsCameraRunning)
                {
                    await StopCameraAsync();
                    await Task.Delay(500);
                }
            });
        }

        #endregion

        #region Device Management

        private async Task LoadAvailableDevicesAsync()
        {
            await SafeExecuteAsync("Load Devices", async () =>
            {
                _isLoadingDevices = true; // FIX: Block selection events

                var devices = (await _cameraService.GetAvailableDevicesAsync()).ToList();

                DeviceComboBox.Items.Clear();

                // FIX: Disable the flag BEFORE showing devices so SelectionChanged can fire
                _isLoadingDevices = false;

                if (devices.Count == 0)
                {
                    ShowNoDevicesState();
                }
                else
                {
                    ShowDevicesAvailableState(devices);
                }

                UpdateLastAction();
            });
        }

        #endregion

        #region Camera Control

        private async Task StartCameraAsync()
        {
            // FIX: Check if device needs initialization
            if (_cameraService.SelectedDevice == null)
            {
                if (DeviceComboBox.SelectedItem != null)
                {
                    await SelectDeviceAsync();
                }
                else
                {
                    CameraStatusText.Text = "No Device Selected";
                    return;
                }
            }
            
            var result = await _cameraService.StartCameraAsync();

            if (result)
            {
                // Give hardware time to start producing frames
                await Task.Delay(200);
                StartFrameCapture();
                UpdateUIForRunningCamera();
            }
            else
            {
                CameraStatusText.Text = "Failed to Start";
            }
        }

        private async Task StopCameraAsync()
        {
            await CancelFrameCaptureAsync();
            await WaitForFrameTaskCompletionAsync();
            await StopCameraHardwareAsync();
            await UpdateUIAfterStopAsync();
            CleanupFrameCaptureResources();
            UpdateUIForStoppedCamera();
        }

        private async Task CancelFrameCaptureAsync()
        {
            if (_frameCaptureTokenSource != null)
            {
                await _frameCaptureTokenSource.CancelAsync();
            }
        }

        private async Task WaitForFrameTaskCompletionAsync()
        {
            if (_frameRenderTask != null && !_frameRenderTask.IsCompleted)
            {
                await Task.WhenAny(_frameRenderTask, Task.Delay(STOP_TIMEOUT_MS));
            }
        }

        private async Task StopCameraHardwareAsync()
        {
            await _cameraService.StopCameraAsync();
            await Task.Delay(100);
        }

        private async Task UpdateUIAfterStopAsync()
        {
            await Dispatcher.InvokeAsync(ClearCameraPreview);
        }

        #endregion

        #region Frame Capture

        private void StartFrameCapture()
        {
            if (IsFrameCaptureRunning())
            {
                LogDebug("Previous frame capture still running");
                return;
            }

            InitializeNewFrameCapture();
            LogDebug("Frame capture started");
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
                int failureCount = 0;
                int frameCount = 0;
                const int MAX_CONSECUTIVE_FAILURES = 10;

                while (ShouldContinueCapture(cancellationToken))
                {
                    try
                    {
                        await ProcessSingleFrameCycleAsync(cancellationToken);
                        frameCount++;
                        if (frameCount % 30 == 0) // Log every 30 frames
                        {
                            LogDebug($"Frame capture: {frameCount} frames processed");
                        }
                        failureCount = 0; // Reset on success
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        LogError($"Frame cycle (attempt {failureCount})", ex);

                        if (failureCount >= MAX_CONSECUTIVE_FAILURES || ShouldExitOnError(cancellationToken))
                        {
                            throw new OperationCanceledException("Too many frame capture failures", ex);
                        }

                        await Task.Delay(ERROR_RETRY_DELAY_MS, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogDebug("Frame capture CANCELLED");
            }
            catch (Exception ex)
            {
                LogError("Frame capture loop", ex);
            }
            finally
            {
                LogDebug("Frame capture loop COMPLETED");
            }
        }

        private async Task ProcessSingleFrameCycleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frameBytes = await CaptureFrameBytesAsync();

            if (IsValidFrameData(frameBytes))
            {
                await ConvertAndDisplayFrameAsync(frameBytes!, cancellationToken);
                // Only delay if we successfully captured a frame
                await DelayForNextFrameAsync(cancellationToken);
            }
            else
            {
                // If no frame available, wait a bit before retry
                await Task.Delay(10, cancellationToken);
            }
        }

        private async Task<byte[]?> CaptureFrameBytesAsync()
        {
            var frame = await _cameraService.GetCurrentFrameAsync();
            if (frame == null)
            {
                LogDebug("No frame data received");
            }
            return frame;
        }

        private async Task ConvertAndDisplayFrameAsync(byte[] frameBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bitmap = CreateBitmapFromBytes(frameBytes);
            if (bitmap == null)
            {
                LogDebug("Failed to create bitmap from frame bytes");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            await UpdateCameraPreviewAsync(bitmap, cancellationToken);
            LogDebug("Frame displayed successfully");
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

        private async Task UpdateCameraPreviewAsync(BitmapImage bitmap, CancellationToken cancellationToken)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (ShouldContinueCapture(cancellationToken))
                {
                    ShowCameraPreview(bitmap);
                }
            });
        }

        private void ShowCameraPreview(BitmapImage bitmap)
        {
            CameraPlaceholder.Visibility = Visibility.Collapsed;
            CameraPreview.Source = bitmap;
            CameraPreview.Visibility = Visibility.Visible;
        }

        private static async Task DelayForNextFrameAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(FRAME_DELAY_MS, cancellationToken);
        }

        private bool ShouldExitOnError(CancellationToken cancellationToken)
        {
            return !_cameraService.IsCameraRunning || cancellationToken.IsCancellationRequested;
        }

        private bool ShouldContinueCapture(CancellationToken cancellationToken)
        {
            return !cancellationToken.IsCancellationRequested && _cameraService.IsCameraRunning;
        }

        private static bool IsValidFrameData(byte[]? frameBytes)
        {
            return frameBytes != null && frameBytes.Length > 0;
        }

        private void CleanupFrameCaptureResources()
        {
            try
            {
                _frameCaptureTokenSource?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                LogDebug("Frame capture token source already disposed");
            }

            _frameCaptureTokenSource = null;
            _frameRenderTask = null;
        }

        #endregion

        #region Event Handlers

        private void ClipGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            GridClip.Rect = new Rect(0, 0, ClipGrid.ActualWidth, ClipGrid.ActualHeight);
        }

        private async void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!CanHandleDeviceSelection())
            {
                return;
            }

            await ExecuteWithLock("Device Selection", async () =>
            {
                await PerformDeviceSelectionAsync();
            });
        }

        private bool CanHandleDeviceSelection()
        {
            if (_isLoadingDevices)
            {
                return false;
            }
            
            if (DeviceComboBox.SelectedItem == null || !DeviceComboBox.IsEnabled)
            {
                return false;
            }

            if (_isOperationInProgress)
            {
                LogDebug("Operation in progress, ignoring selection");
                return false;
            }

            return true;
        }

        private async Task PerformDeviceSelectionAsync()
        {
            DisableControls();

            if (_cameraService.IsCameraRunning)
            {
                LogDebug("Stopping camera for device switch");
                await StopCameraAsync();
                await Task.Delay(DEVICE_RELEASE_DELAY_MS);
            }

            await SelectDeviceAsync();
            await Task.Delay(200);
            EnableControls();
        }

        private async Task SelectDeviceAsync()
        {
            var deviceName = DeviceComboBox.SelectedItem.ToString();
            if (string.IsNullOrEmpty(deviceName))
            {
                return;
            }

            LogDebug($"Selecting device: {deviceName}");
            var result = await _cameraService.SelectDeviceAsync(deviceName);

            if (result)
            {
                LogDebug($"Device selected: {deviceName}");
                UpdateLastAction();
                UpdateCameraStatus();
            }
            else
            {
                LastActionText.Text = $"Failed to select: {deviceName}";
            }
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanExecuteOperation())
            {
                return;
            }

            await ExecuteWithLock("Camera Toggle", async () =>
            {
                await ToggleCameraAsync();
            });
        }

        private async Task ToggleCameraAsync()
        {
            StartStopButton.IsEnabled = false;

            if (_cameraService.IsCameraRunning)
            {
                await StopCameraAsync();
            }
            else
            {
                await StartCameraAsync();
            }

            UpdateLastAction();
            UpdateCameraStatus();

            StartStopButton.IsEnabled = true;
        }

        private async void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isOperationInProgress || _isLoadingDevices)
            {
                LogDebug("Operation in progress, ignoring refresh");
                return;
            }

            await SafeExecuteAsync("Refresh Devices", async () =>
            {
                await PerformDeviceRefreshAsync();
            });
        }

        private async Task PerformDeviceRefreshAsync()
        {
            LastActionText.Text = "Refreshing device list...";
            DisableAllControls();

            if (_cameraService.IsCameraRunning)
            {
                await StopCameraAsync();
                await Task.Delay(DEVICE_RELEASE_DELAY_MS); // Wait for device release
            }

            ClearCameraPreview();
            await Task.Delay(200);
            
            await LoadAvailableDevicesAsync();
            LastActionText.Text = "Device list refreshed";

            EnableAllControls();
        }

        #endregion

        #region UI Update Methods

        private void ShowNoDevicesState()
        {
            DeviceComboBox.IsEnabled = false;
            DeviceComboBox.SelectedItem = null;
            NoDevicesMessage.Visibility = Visibility.Visible;
            CameraStatusText.Text = "No Devices Available";
            LogDebug("No camera devices found");
        }

        private void ShowDevicesAvailableState(List<string> devices)
        {
            NoDevicesMessage.Visibility = Visibility.Collapsed;
            DeviceComboBox.IsEnabled = true;

            foreach (var device in devices)
            {
                DeviceComboBox.Items.Add(device);
            }

            if (DeviceComboBox.Items.Count > 0)
            {
                DeviceComboBox.SelectedIndex = 0;
            }

            LogDebug($"Loaded {devices.Count} camera devices");
        }

        private void UpdateUIForRunningCamera()
        {
            StartStopButton.Content = "Stop Camera";
            CameraStatusText.Text = "Camera Running";
        }

        private void UpdateUIForStoppedCamera()
        {
            StartStopButton.Content = "Start Camera";
            CameraStatusText.Text = "Camera Stopped";
        }

        private void ClearCameraPreview()
        {
            CameraPlaceholder.Visibility = Visibility.Visible;
            CameraPreview.Source = null;
            CameraPreview.Visibility = Visibility.Collapsed;
        }

        private void UpdateLastAction()
        {
            LastActionText.Text = _cameraService.LastAction;
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
                    CameraStatusText.Text = "Camera Ready";
                    StartStopButton.Content = "Start Camera";
                }
            }
            else
            {
                CameraStatusText.Text = "No Device Selected";
                StartStopButton.Content = "Start Camera";
            }
        }

        private void DisableControls()
        {
            DeviceComboBox.IsEnabled = false;
            StartStopButton.IsEnabled = false;
        }

        private void EnableControls()
        {
            DeviceComboBox.IsEnabled = true;
            StartStopButton.IsEnabled = true;
        }

        private void DisableAllControls()
        {
            RefreshDevicesButton.IsEnabled = false;
            StartStopButton.IsEnabled = false;
            DeviceComboBox.IsEnabled = false;
        }

        private void EnableAllControls()
        {
            RefreshDevicesButton.IsEnabled = true;
            StartStopButton.IsEnabled = true;
            DeviceComboBox.IsEnabled = DeviceComboBox.Items.Count > 0;
        }

        #endregion

        #region Helper Methods

        private async Task SafeExecuteAsync(string operationName, Func<Task> operation)
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                LogError(operationName, ex);
                LastActionText.Text = $"Error in {operationName}: {ex.Message}";
            }
        }

        private async Task ExecuteWithLock(string operationName, Func<Task> operation)
        {
            try
            {
                _isOperationInProgress = true;
                _lastOperationTime = DateTime.UtcNow;
                await operation();
            }
            catch (Exception ex)
            {
                LogError(operationName, ex);
                LastActionText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _isOperationInProgress = false;
            }
        }

        private bool CanExecuteOperation()
        {
            if (_isOperationInProgress)
            {
                LogDebug("Operation in progress, ignoring");
                return false;
            }

            var timeSinceLastOperation = (DateTime.UtcNow - _lastOperationTime).TotalMilliseconds;
            if (timeSinceLastOperation < OPERATION_COOLDOWN_MS)
            {
                LogDebug($"Click too fast ({timeSinceLastOperation:F0}ms), ignoring");
                return false;
            }

            return true;
        }

        private static void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[CameraTestPage] {message}");
        }

        private static void LogError(string operation, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CameraTestPage] Error in {operation}: {ex.GetType().Name}: {ex.Message}");
        }

        #endregion
    }
}