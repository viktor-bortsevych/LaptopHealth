using LaptopHealth.Services.Interfaces;
using OpenCvSharp;
using System.Management;
using IApplicationLogger = LaptopHealth.Services.Interfaces.ILogger;

namespace LaptopHealth.Services.Hardware
{
    public class CameraOpenCvService(IApplicationLogger logger) : ICameraHardwareService, IDisposable
    {
        #region Fields & Constants

        private VideoCapture? _capture;
        private bool _isCapturing;
        private Mat? _currentFrame;
        private readonly Lock _frameAccessLock = new(); // Only for frame operations
        private readonly Lock _operationCtsLock = new(); // For _currentOperationCts access
        private bool _disposed = false;
        private readonly Dictionary<int, string> _deviceNameCache = [];

        // Sequential operation execution
        private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
        private CancellationTokenSource? _currentOperationCts;

        private const int MAX_DEVICE_INDEX = 20;
        private const int STANDARD_DEVICE_INDEX = 5;

        public bool IsCapturing => _isCapturing;

        #endregion

        #region Public API

        public Task<IEnumerable<string>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    logger.Section("CAMERA DEVICE ENUMERATION STARTED");
                    PrintEnumerationInfo();

                    // Pre-populate device name cache with real Windows device names
                    PopulateDeviceNameCache();

                    var devices = DetectDevicesWithBackends();

                    if (devices.Count == 0)
                    {
                        devices = DetectDevicesExtended();
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    PrintEnumerationResult(devices);
                    logger.SectionEnd();

                    return (IEnumerable<string>)devices;
                }
                catch (OperationCanceledException)
                {
                    // Silently handle cancellation - this is expected behavior
                    return [];
                }
                catch (Exception ex)
                {
                    logger.Error("Device enumeration", ex);
                    return [];
                }
            }, cancellationToken);
        }

        public Task<bool> InitializeDeviceAsync(string deviceName, CancellationToken cancellationToken = default)
        {
            return ExecuteSequentiallyAsync(async (opCt) =>
            {
                try
                {
                    logger.Debug($"InitializeDeviceAsync: Initializing device {deviceName}");

                    // Stop any current capture first
                    logger.Debug("InitializeDeviceAsync: Stopping any current capture");
                    await StopCaptureInternalAsync(opCt);

                    _isCapturing = false;
                    logger.Debug("InitializeDeviceAsync: Cleaning up old capture");
                    CleanupCapture();

                    // Create new VideoCapture with proper initialization
                    int deviceIndex = ExtractDeviceIndex(deviceName);
                    logger.Debug($"InitializeDeviceAsync: Creating VideoCapture for device index {deviceIndex}");

                    _capture = CreateVideoCapture(deviceIndex);

                    logger.Debug($"InitializeDeviceAsync: VideoCapture created, checking if opened...");
                    bool isOpened = _capture.IsOpened();
                    logger.Debug($"InitializeDeviceAsync: VideoCapture IsOpened = {isOpened}");

                    if (!isOpened)
                    {
                        logger.Error($"InitializeDeviceAsync: Failed to open device {deviceName}");
                        CleanupCapture();
                        return false;
                    }

                    LogDeviceInfo(_capture, deviceName);
                    logger.Info($"InitializeDeviceAsync: Device {deviceName} initialized successfully");
                    return true;
                }
                catch (OperationCanceledException)
                {
                    logger.Warn("InitializeDeviceAsync: Operation cancelled");
                    CleanupCapture();
                    return false;
                }
                catch (Exception ex)
                {
                    logger.Error($"Initialize device: {deviceName}", ex);
                    CleanupCapture();
                    return false;
                }
            }, cancellationToken);
        }

        public Task<bool> StartCaptureAsync(CancellationToken cancellationToken = default)
        {
            logger.Debug($"[StartCaptureAsync] Called with cancellation token: {cancellationToken.GetHashCode()}");

            return ExecuteSequentiallyAsync(async (opCt) =>
            {
                logger.Debug($"[StartCaptureAsync] Inside ExecuteSequentiallyAsync, opCt: {opCt.GetHashCode()}");

                try
                {
                    if (_capture == null || !_capture.IsOpened())
                    {
                        logger.Error($"Cannot start: device not initialized (capture={_capture}, isOpened={_capture?.IsOpened()})");
                        return false;
                    }

                    if (_isCapturing)
                    {
                        logger.Debug("Camera already capturing");
                        return true;
                    }

                    logger.Debug("Setting _isCapturing = true");
                    _isCapturing = true;

                    logger.Debug("About to call WarmupCaptureAsync...");
                    bool warmupSuccess = await WarmupCaptureAsync();
                    logger.Debug($"WarmupCaptureAsync completed with success: {warmupSuccess}");

                    if (!warmupSuccess)
                    {
                        logger.Error("Camera warmup failed - cannot start capture");
                        _isCapturing = false;
                        return false;
                    }

                    logger.Info("Camera capture started successfully");
                    return true;
                }
                catch (OperationCanceledException ex)
                {
                    _isCapturing = false;
                    logger.Warn($"Camera start was cancelled: {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    logger.Error($"Start capture exception: {ex.GetType().Name} - {ex.Message}", ex);
                    _isCapturing = false;
                    return false;
                }
            }, cancellationToken);
        }

        private async Task<bool> WarmupCaptureAsync()
        {
            try
            {
                logger.Debug("Starting camera warmup...");
                logger.Debug($"Capture state: capture={_capture}, isOpened={_capture?.IsOpened()}, isCapturing={_isCapturing}");

                if (_capture == null)
                {
                    logger.Error("WarmupCaptureAsync: capture is null!");
                    return false;
                }

                if (!_capture.IsOpened())
                {
                    logger.Error("WarmupCaptureAsync: capture is not opened!");
                    return false;
                }

                using var warmupMat = new Mat();
                int successfulReads = 0;
                int totalAttempts = 0;

                for (int i = 0; i < 15 && successfulReads < 3; i++)
                {
                    totalAttempts++;

                    try
                    {
                        if (_capture!.Read(warmupMat) && !warmupMat.Empty())
                        {
                            successfulReads++;
                            logger.Debug($"Warmup frame {successfulReads}/3 captured");
                        }
                        else
                        {
                            logger.Debug($"Warmup attempt {totalAttempts}: Read returned false or mat is empty");
                            // Small delay between attempts to give the device time to respond
                            await Task.Delay(10);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug($"Warmup attempt {totalAttempts} exception: {ex.GetType().Name} - {ex.Message}");
                        await Task.Delay(10);
                    }
                }

                if (successfulReads == 0)
                {
                    logger.Error($"Camera warmup FAILED: Could not read any frames after {totalAttempts} attempts");
                    return false;
                }
                else
                {
                    logger.Debug($"Camera warmup SUCCESSFUL: {successfulReads} frames in {totalAttempts} attempts");
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Camera warmup exception: {ex.GetType().Name} - {ex.Message}", ex);
                return false;
            }
        }

        public Task<bool> StopCaptureAsync()
        {
            return ExecuteSequentiallyAsync(async (opCt) =>
            {
                return await StopCaptureInternalAsync(opCt);
            });
        }

        private Task<bool> StopCaptureInternalAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    logger.Debug($"StopCaptureInternalAsync: isCapturing={_isCapturing}");

                    if (!_isCapturing)
                    {
                        logger.Debug("StopCaptureInternalAsync: Not capturing, returning true");
                        return true;
                    }

                    logger.Debug("StopCaptureInternalAsync: Setting _isCapturing = false");
                    _isCapturing = false;

                    logger.Debug("StopCaptureInternalAsync: Disposing frame");
                    DisposeFrame();

                    logger.Debug("StopCaptureInternalAsync: Completed");
                    return true;
                }
                catch (Exception ex)
                {
                    logger.Error("Stop capture", ex);
                    return false;
                }
            }, cancellationToken);
        }

        public Task<byte[]?> GetCurrentFrameAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run<byte[]?>(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    lock (_frameAccessLock)
                    {
                        if (!CanCaptureFrame())
                        {
                            if (_capture == null)
                            {
                                logger.Debug("GetCurrentFrameAsync: capture is null");
                            }
                            else if (!_isCapturing)
                            {
                                logger.Debug("GetCurrentFrameAsync: not capturing");
                            }
                            else if (!_capture.IsOpened())
                            {
                                logger.Debug("GetCurrentFrameAsync: capture not opened");
                            }
                            return null;
                        }

                        EnsureFrameMatExists();

                        if (!ReadFrameFromCapture())
                        {
                            logger.Debug("GetCurrentFrameAsync: failed to read frame from capture");
                            return null;
                        }

                        var bytes = EncodeFrameToBytes();
                        if (bytes == null || bytes.Length == 0)
                        {
                            logger.Debug("GetCurrentFrameAsync: encoded frame is empty");
                            return null;
                        }

                        return bytes;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Silently handle cancellation - this is expected behavior
                    return null;
                }
                catch (Exception ex)
                {
                    logger.Error("Get frame", ex);
                    return null;
                }
            }, cancellationToken);
        }

        public Task DisposeAsync()
        {
            return Task.Run(() => Dispose());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _isCapturing = false;
                ReleaseAllResources();
                _deviceNameCache.Clear();
                _operationSemaphore?.Dispose();
                _currentOperationCts?.Dispose();
                _currentOperationCts = null;
                logger.Info("Disposed camera resources");
            }

            _disposed = true;
        }

        #endregion

        #region Sequential Operation Execution

        /// <summary>
        /// Executes an operation sequentially, ensuring only one operation runs at a time.
        /// Cancels any previous operation and waits for the semaphore to be available.
        /// </summary>
        private async Task<T> ExecuteSequentiallyAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken userCancellationToken = default)
        {
            logger.Debug($"[ExecuteSequentiallyAsync] Entering, userCt: {userCancellationToken.GetHashCode()}");

            // Cancel previous operation
            CancellationTokenSource? previousCts = null;
            lock (_operationCtsLock)
            {
                if (_currentOperationCts != null)
                {
                    logger.Debug("[ExecuteSequentiallyAsync] Cancelling previous operation");
                    previousCts = _currentOperationCts;
                }
            }

            if (previousCts != null)
            {
                await previousCts.CancelAsync();
                lock (_operationCtsLock)
                {
                    previousCts.Dispose();
                    _currentOperationCts = null;
                }
            }

            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCancellationToken);
            lock (_operationCtsLock)
            {
                _currentOperationCts = linkedCts;
                logger.Debug($"[ExecuteSequentiallyAsync] Created linked token: {linkedCts.Token.GetHashCode()}");
            }

            logger.Debug("[ExecuteSequentiallyAsync] Waiting for semaphore...");
            await _operationSemaphore.WaitAsync(userCancellationToken);
            logger.Debug("[ExecuteSequentiallyAsync] Semaphore acquired");

            try
            {
                logger.Debug("[ExecuteSequentiallyAsync] Executing operation...");
                var result = await operation(linkedCts.Token);
                logger.Debug($"[ExecuteSequentiallyAsync] Operation completed with result: {result}");
                return result;
            }
            catch (OperationCanceledException ex)
            {
                logger.Warn($"[ExecuteSequentiallyAsync] Operation was cancelled: {ex.Message}");
                throw;
            }
            finally
            {
                logger.Debug("[ExecuteSequentiallyAsync] Releasing semaphore");
                _operationSemaphore.Release();

                // Clean up operation token source after operation completes
                lock (_operationCtsLock)
                {
                    if (_currentOperationCts == linkedCts)
                    {
                        _currentOperationCts = null;
                    }
                }

                // Dispose linked CTS - this must happen AFTER all operations are complete
                try
                {
                    linkedCts?.Dispose();
                }
                catch (Exception ex)
                {
                    logger.Warn($"[ExecuteSequentiallyAsync] Error disposing linked CTS: {ex.Message}");
                }
            }
        }

        #endregion

        #region Device Detection

        /// <summary>
        /// Populates the device name cache with actual Windows camera device names via WMI
        /// </summary>
        private void PopulateDeviceNameCache()
        {
            try
            {
                var deviceNames = GetWindowsCameraDeviceNames();
                if (deviceNames.Count > 0)
                {
                    logger.Debug($"Found {deviceNames.Count} camera device names via WMI");
                    for (int i = 0; i < deviceNames.Count; i++)
                    {
                        _deviceNameCache[i] = deviceNames[i];
                        logger.Debug($"Cached device {i}: {deviceNames[i]}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"Failed to populate device name cache via WMI: {ex.GetType().Name}");
            }
        }

        /// <summary>
        /// Queries Windows for actual camera device names using WMI
        /// </summary>
        private List<string> GetWindowsCameraDeviceNames()
        {
            var deviceNames = new List<string>();

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%camera%' OR Name LIKE '%webcam%'");
                var collection = searcher.Get();

                deviceNames.AddRange(collection
                    .Cast<ManagementObject>()
                    .Select(device => device["Name"]?.ToString())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name =>
                    {
                        logger.Debug($"WMI discovered: {name}");
                        return name!;
                    }));
            }
            catch (Exception ex)
            {
                logger.Debug($"WMI query error: {ex.GetType().Name} - {ex.Message}");
            }

            return deviceNames;
        }

        private List<string> DetectDevicesWithBackends()
        {
            var backends = new[] { "Default (Auto)", "DSHOW", "WinRT", "FFMPEG" };

            return backends
                .Select(backendName => TryDetectWithBackend(backendName))
                .FirstOrDefault(detected => detected.Count > 0) ?? [];
        }

        private List<string> TryDetectWithBackend(string backendName)
        {
            var devices = new List<string>();
            logger.Debug($"Trying backend: {backendName}");

            for (int i = 0; i < STANDARD_DEVICE_INDEX; i++)
            {
                var deviceName = TryDetectDeviceAtIndex(i);
                if (deviceName != null)
                {
                    devices.Add(deviceName);
                }
            }

            return devices;
        }

        private string? TryDetectDeviceAtIndex(int index)
        {
            try
            {
                using var testCapture = new VideoCapture(index);

                if (!testCapture.IsOpened())
                {
                    return null;
                }

                var (width, height) = GetResolution(testCapture);

                if (width > 0 && height > 0)
                {
                    logger.Debug($"Index {index}: {width}x{height}");
                    return GetDeviceName(index);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private List<string> DetectDevicesExtended()
        {
            var devices = new List<string>();
            logger.Debug("No devices found with standard detection.");
            logger.Debug("Attempting extended index enumeration (0-20)...\n");

            for (int i = 0; i < MAX_DEVICE_INDEX; i++)
            {
                var deviceName = TryDetectDeviceExtended(i);
                if (deviceName != null)
                {
                    devices.Add(deviceName);
                }
            }

            return devices;
        }

        private string? TryDetectDeviceExtended(int index)
        {
            try
            {
                using var testCapture = new VideoCapture(index);
                logger.Debug($"[{index,2}] Testing... IsOpened={testCapture.IsOpened()}");

                if (!testCapture.IsOpened())
                {
                    return null;
                }

                var (width, height) = GetResolution(testCapture);
                var fps = testCapture.Get(VideoCaptureProperties.Fps);
                var backend = testCapture.Get(VideoCaptureProperties.Backend);

                logger.Debug($"[{index,2}] FOUND! Resolution: {width}x{height}, FPS: {fps}, Backend: {backend}");

                return (width > 0 && height > 0) ? GetDeviceName(index) : null;
            }
            catch (Exception ex)
            {
                logger.Debug($"[{index,2}] Exception: {ex.GetType().Name}");
                return null;
            }
        }

        #endregion

        #region Initialization Helpers

        private static VideoCapture CreateVideoCapture(int deviceIndex)
        {
            return new VideoCapture(deviceIndex);
        }

        private static bool ValidateCapture(VideoCapture capture)
        {
            return capture.IsOpened();
        }

        #endregion

        #region Capture Operations

        private bool CanCaptureFrame()
        {
            return _capture != null && _isCapturing && _capture.IsOpened();
        }

        private void EnsureFrameMatExists()
        {
            _currentFrame ??= new Mat();
        }

        private bool ReadFrameFromCapture()
        {
            _currentFrame ??= new Mat();

            if (!_capture!.Read(_currentFrame))
            {
                return false;
            }

            return !_currentFrame.Empty();
        }

        private byte[] EncodeFrameToBytes()
        {
            return _currentFrame!.ImEncode(".png");
        }

        #endregion

        #region Resource Management

        private void ReleaseAllResources()
        {
            DisposeFrame();
            CleanupCapture();
        }

        private void DisposeFrame()
        {
            if (_currentFrame != null)
            {
                TryDispose(_currentFrame, "frame");
                _currentFrame = null;
            }
        }

        private void CleanupCapture()
        {
            if (_capture != null)
            {
                logger.Debug($"CleanupCapture: Releasing capture, IsOpened={_capture.IsOpened()}");
                TryReleaseCapture(_capture);
                TryDispose(_capture, "capture");
                _capture = null;
                logger.Debug("CleanupCapture: Capture cleanup completed");
            }
        }

        private void TryReleaseCapture(VideoCapture capture)
        {
            try
            {
                logger.Debug("TryReleaseCapture: Calling Release()");
                capture.Release();
                logger.Debug("TryReleaseCapture: Release() completed");
            }
            catch (Exception ex)
            {
                logger.Warn($"Warning releasing capture: {ex.GetType().Name} - {ex.Message}");
            }
        }

        private void TryDispose(IDisposable resource, string resourceName)
        {
            try
            {
                logger.Debug($"TryDispose: Disposing {resourceName}");
                resource.Dispose();
                logger.Debug($"TryDispose: {resourceName} disposed successfully");
            }
            catch (Exception ex)
            {
                logger.Warn($"Warning disposing {resourceName}: {ex.GetType().Name} - {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        private static int ExtractDeviceIndex(string deviceName)
        {
            return int.TryParse(deviceName.Split(' ').LastOrDefault(), out int index) ? index : 0;
        }

        /// <summary>
        /// Gets the device name for a given index. First checks the cache for real Windows device names,
        /// then falls back to a generic name if not found.
        /// </summary>
        private string GetDeviceName(int index)
        {
            if (_deviceNameCache.TryGetValue(index, out var cachedName))
            {
                return $"{cachedName} [{index}]";
            }

            return $"Camera {index}";
        }

        private static (double width, double height) GetResolution(VideoCapture capture)
        {
            return (
                capture.Get(VideoCaptureProperties.FrameWidth),
                capture.Get(VideoCaptureProperties.FrameHeight)
            );
        }

        #endregion

        #region Logging Helpers

        private void PrintEnumerationInfo()
        {
            logger.Debug($"Environment: {Environment.OSVersion}");
            logger.Debug($".NET Version: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            logger.Debug("OpenCV Backend: Checking available...");
        }

        private void PrintEnumerationResult(List<string> devices)
        {
            logger.Info($"ENUMERATION COMPLETE: Found {devices.Count} device(s)");

            if (devices.Count == 0)
            {
                logger.Troubleshoot("NO CAMERA DEVICES DETECTED");
            }
            else
            {
                foreach (var device in devices)
                {
                    logger.Info($"Available: {device}");
                }
            }
        }

        private void LogDeviceInfo(VideoCapture capture, string deviceName)
        {
            var (width, height) = GetResolution(capture);
            var fps = capture.Get(VideoCaptureProperties.Fps);

            logger.Debug($"Successfully initialized device: {deviceName}");
            logger.Debug($"  Resolution: {width}x{height}");
            logger.Debug($"  FPS: {fps}");
        }

        #endregion
    }
}