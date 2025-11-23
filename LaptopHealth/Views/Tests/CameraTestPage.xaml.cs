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
        
        // New operation manager
        private readonly SemaphoreSlim _uiOperationLock = new(1, 1);
        private CancellationTokenSource? _currentOperationCts;
        
        private bool _isLoadingDevices = false;

        private const int FRAME_DELAY_MS = 33;
        private const int STOP_TIMEOUT_MS = 2000;
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
            try
            {
                if (_cameraService.IsCameraRunning)
                {
                    _currentOperationCts?.Cancel();
                    await StopCameraAsync(CancellationToken.None);
                }
                
                _uiOperationLock.Dispose();
                _currentOperationCts?.Dispose();
            }
            catch (Exception ex)
            {
                LogError("Page Cleanup", ex);
            }
        }

        #endregion

        #region Device Management

        private async Task LoadAvailableDevicesAsync(CancellationToken ct = default)
        {
            _isLoadingDevices = true; // Block SelectionChanged

            try
            {
                var devices = (await _cameraService.GetAvailableDevicesAsync()).ToList();

                DeviceComboBox.Items.Clear();

                if (devices.Count == 0)
                {
                    ShowNoDevicesState();
                }
                else
                {
                    ShowDevicesAvailableState(devices);
                    
                    // Auto-select first device (but DON'T auto-start - let user click Start button)
                    DeviceComboBox.SelectedIndex = 0;
                }

                UpdateLastAction();
            }
            finally
            {
                _isLoadingDevices = false; // Re-enable SelectionChanged
            }
        }

        #endregion

        #region Camera Control

        private async Task StartCameraAsync(CancellationToken ct)
        {
            // Ensure device is selected
            if (_cameraService.SelectedDevice == null)
            {
                if (DeviceComboBox.SelectedItem != null)
                {
                    var deviceName = DeviceComboBox.SelectedItem.ToString()!;
                    await _cameraService.SelectDeviceAsync(deviceName);
                }
                else
                {
                    CameraStatusText.Text = "No Device Selected";
                    return;
                }
            }
            
            ct.ThrowIfCancellationRequested();
            
            // Start camera (service handles initialization internally)
            var result = await _cameraService.StartCameraAsync();
            
            if (result)
            {
                StartFrameCapture(); // Immediately start rendering
                UpdateUIForRunningCamera();
            }
            else
            {
                CameraStatusText.Text = "Failed to Start";
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
            await Dispatcher.InvokeAsync(ClearCameraPreview);
            
            // Cleanup
            _frameCaptureTokenSource?.Dispose();
            _frameCaptureTokenSource = null;
            _frameRenderTask = null;
            
            UpdateUIForStoppedCamera();
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
                while (!cancellationToken.IsCancellationRequested && _cameraService.IsCameraRunning)
                {
                    try
                    {
                        var frameBytes = await _cameraService.GetCurrentFrameAsync();
                        
                        if (frameBytes != null && frameBytes.Length > 0)
                        {
                            var bitmap = CreateBitmapFromBytes(frameBytes);
                            
                            if (bitmap != null)
                            {
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    if (!cancellationToken.IsCancellationRequested)
                                    {
                                        ShowCameraPreview(bitmap);
                                    }
                                });
                            }
                        }
                        
                        // Target ~30 FPS
                        await Task.Delay(FRAME_DELAY_MS, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogError("Frame capture", ex);
                        await Task.Delay(ERROR_RETRY_DELAY_MS, cancellationToken); // Brief pause on error
                    }
                }
            }
            finally
            {
                LogDebug("Frame capture loop COMPLETED");
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

        private async Task UpdateCameraPreviewAsync(BitmapImage bitmap, CancellationToken cancellationToken)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
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

        private void CleanupFrameCaptureResources()
        {
            try
            {
                _frameCaptureTokenSource?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed - this is fine
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
            // Ignore if loading or no selection
            if (_isLoadingDevices || DeviceComboBox.SelectedItem == null)
                return;
            
            try
            {
                await ExecuteCameraOperationAsync(async ct =>
                {
                    var deviceName = DeviceComboBox.SelectedItem.ToString()!;
                    
                    // Stop current camera if running
                    if (_cameraService.IsCameraRunning)
                    {
                        await StopCameraAsync(ct);
                    }
                    
                    // Select new device (no delay needed!)
                    await _cameraService.SelectDeviceAsync(deviceName);
                    
                    LogDebug($"Device switched to: {deviceName}");
                    UpdateCameraStatus();
                    UpdateLastAction();
                    
                }, "Switch Device");
            }
            catch (OperationCanceledException)
            {
                // User selected another device - this operation cancelled
            }
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
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
            catch (OperationCanceledException)
            {
                // User clicked again - old operation cancelled, new one starting
            }
        }

        private async void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ExecuteCameraOperationAsync(async ct =>
                {
                    LastActionText.Text = "Refreshing devices...";
                    
                    // Stop camera if running
                    if (_cameraService.IsCameraRunning)
                    {
                        await StopCameraAsync(ct);
                    }
                    
                    ClearCameraPreview();
                    
                }, "Stop Camera Before Refresh");
                
                // Load devices OUTSIDE the cancellable operation
                // This prevents device selection from being interrupted
                await LoadAvailableDevicesAsync();
                
                // Auto-select and initialize first device (but don't start)
                if (DeviceComboBox.Items.Count > 0 && DeviceComboBox.SelectedItem != null)
                {
                    var deviceName = DeviceComboBox.SelectedItem.ToString()!;
                    await _cameraService.SelectDeviceAsync(deviceName);
                    LogDebug($"Device re-initialized after refresh: {deviceName}");
                }
                
                LastActionText.Text = "Devices refreshed";
            }
            catch (OperationCanceledException) { }
        }

        #endregion

        #region UI Update Methods

        private void ShowNoDevicesState()
        {
            DeviceComboBox.IsEnabled = false;
            DeviceComboBox.SelectedItem = null;
            NoDevicesMessage.Visibility = Visibility.Visible;
            CameraStatusText.Text = "No Devices Available";
        }

        private void ShowDevicesAvailableState(List<string> devices)
        {
            NoDevicesMessage.Visibility = Visibility.Collapsed;
            DeviceComboBox.IsEnabled = true;

            foreach (var device in devices)
            {
                DeviceComboBox.Items.Add(device);
            }

            LogDebug($"Found {devices.Count} device(s)");
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
            // Disable UI
            StartStopButton.IsEnabled = false;
            DeviceComboBox.IsEnabled = false;
            RefreshDevicesButton.IsEnabled = false;
            
            // Cancel any in-progress operation
            _currentOperationCts?.Cancel();
            
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
                
                // Re-enable UI
                StartStopButton.IsEnabled = true;
                DeviceComboBox.IsEnabled = DeviceComboBox.Items.Count > 0;
                RefreshDevicesButton.IsEnabled = true;
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
            System.Diagnostics.Debug.WriteLine($"[CameraTestPage] {message}");
        }

        private static void LogError(string operation, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CameraTestPage] Error in {operation}: {ex.Message}");
        }

        #endregion
    }
}