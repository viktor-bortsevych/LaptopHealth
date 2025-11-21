using LaptopHealth.Services.Interfaces;
using OpenCvSharp;
using System.Diagnostics;

namespace LaptopHealth.Services.Hardware
{
    public class CameraOpenCvService : ICameraHardwareService, IDisposable
    {
        #region Fields & Constants

        private VideoCapture? _capture;
        private bool _isCapturing;
        private string? _currentDevice;
        private Mat? _currentFrame;
        private readonly Lock _lockObject = new();
        private bool _disposed = false;

        private const int MAX_DEVICE_INDEX = 20;
        private const int STANDARD_DEVICE_INDEX = 5;
        private const int RESOURCE_DELAY_MS = 100;
        private const int STOP_DELAY_MS = 50;

        public bool IsCapturing => _isCapturing;

        #endregion

        #region Public API

        public Task<IEnumerable<string>> GetAvailableDevicesAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    PrintEnumerationHeader();

                    var devices = DetectDevicesWithBackends();

                    if (devices.Count == 0)
                    {
                        devices = DetectDevicesExtended();
                    }

                    PrintEnumerationFooter(devices);

                    return (IEnumerable<string>)devices;
                }
                catch (Exception ex)
                {
                    LogError("Device enumeration", ex);
                    return [];
                }
            });
        }

        public Task<bool> InitializeDeviceAsync(string deviceName)
        {
            return Task.Run(() =>
            {
                try
                {
                    lock (_lockObject)
                    {
                        Log($"Initializing device: {deviceName}");

                        PrepareForInitialization();

                        int deviceIndex = ExtractDeviceIndex(deviceName);
                        _capture = CreateVideoCapture(deviceIndex);

                        if (!ValidateCapture(_capture))
                        {
                            CleanupAfterFailedInitialization();
                            return false;
                        }

                        _currentDevice = deviceName;
                        
                        // Give the camera time to settle after initialization
                        Thread.Sleep(100);
                        
                        LogDeviceInfo(_capture, deviceName);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Initialize device: {deviceName}", ex);
                    CleanupAfterFailedInitialization();
                    return false;
                }
            });
        }

        public Task<bool> StartCaptureAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    lock (_lockObject)
                    {
                        ValidateCaptureReady();

                        if (_isCapturing)
                        {
                            Log("ℹ Already capturing");
                            return true;
                        }

                        _isCapturing = true;
                        Log($"✓ Starting capture on {_currentDevice}");
                        
                        // Warm up the camera by trying to read initial frames
                        WarmupCapture();
                        
                        return true;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Log($"✗ {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    LogError("Start capture", ex);
                    return false;
                }
            });
        }

        private void WarmupCapture()
        {
            try
            {
                var warmupMat = new Mat();
                int successfulReads = 0;
                
                for (int i = 0; i < 10; i++)
                {
                    if (_capture!.Read(warmupMat) && !warmupMat.Empty())
                    {
                        successfulReads++;
                        Log($"Warmup: Frame {successfulReads} read successfully");
                        
                        if (successfulReads >= 2)
                        {
                            break;
                        }
                    }
                    else
                    {
                        Log($"Warmup: Frame {i + 1} failed, retrying...");
                    }
                    
                    Thread.Sleep(20);
                }
                
                if (successfulReads == 0)
                {
                    Log("⚠ Warmup failed: Could not read any frames");
                }
                
                warmupMat.Dispose();
            }
            catch (Exception ex)
            {
                Log($"Warmup warning: {ex.GetType().Name} - {ex.Message}");
            }
        }

        public Task<bool> StopCaptureAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    lock (_lockObject)
                    {
                        if (!_isCapturing)
                        {
                            Log("ℹ Already stopped");
                            return true;
                        }

                        _isCapturing = false;
                        Log("✓ Stopping capture");

                        DisposeFrame(); // Only clear the current frame
                        
                        Thread.Sleep(STOP_DELAY_MS);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError("Stop capture", ex);
                    return false;
                }
            });
        }

        public Task<byte[]?> GetCurrentFrameAsync()
        {
            return Task.Run<byte[]?>(() =>
            {
                try
                {
                    lock (_lockObject)
                    {
                        if (!CanCaptureFrame())
                        {
                            return null;
                        }

                        EnsureFrameMatExists();

                        if (!ReadFrameFromCapture())
                        {
                            return null;
                        }

                        return EncodeFrameToBytes();
                    }
                }
                catch (Exception ex)
                {
                    LogError("Get frame", ex);
                    return null;
                }
            });
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
                lock (_lockObject)
                {
                    _isCapturing = false;
                    _currentDevice = null;
                    ReleaseAllResources();
                    Log("✓ Disposed camera resources\n");
                }
            }

            _disposed = true;
        }

        #endregion

        #region Device Detection

        private static List<string> DetectDevicesWithBackends()
        {
            var devices = new List<string>();
            var backends = new[] { "Default (Auto)", "DSHOW", "WinRT", "FFMPEG" };

            foreach (var backendName in backends)
            {
                var detected = TryDetectWithBackend(backendName);
                if (detected.Count > 0)
                {
                    devices.AddRange(detected);
                    break;
                }
            }

            return devices;
        }

        private static List<string> TryDetectWithBackend(string backendName)
        {
            var devices = new List<string>();
            Log($"Trying backend: {backendName}");

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

        private static string? TryDetectDeviceAtIndex(int index)
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
                    Log($"  ✓ Index {index}: {width}x{height}");
                    return GetDeviceName(index);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static List<string> DetectDevicesExtended()
        {
            var devices = new List<string>();
            Log("\nNo devices found with standard detection.");
            Log("Attempting extended index enumeration (0-20)...\n");

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

        private static string? TryDetectDeviceExtended(int index)
        {
            try
            {
                using var testCapture = new VideoCapture(index);
                Log($"[{index,2}] Testing... IsOpened={testCapture.IsOpened()}");

                if (!testCapture.IsOpened())
                {
                    return null;
                }

                var (width, height) = GetResolution(testCapture);
                var fps = testCapture.Get(VideoCaptureProperties.Fps);
                var backend = testCapture.Get(VideoCaptureProperties.Backend);

                Log($"[{index,2}] ✓ FOUND! Resolution: {width}x{height}, FPS: {fps}, Backend: {backend}");

                return (width > 0 && height > 0) ? GetDeviceName(index) : null;
            }
            catch (Exception ex)
            {
                Log($"[{index,2}] Exception: {ex.GetType().Name}");
                return null;
            }
        }

        #endregion

        #region Initialization Helpers

        private void PrepareForInitialization()
        {
            _isCapturing = false;
            ReleaseAllResources();
            Thread.Sleep(RESOURCE_DELAY_MS);
        }

        private static VideoCapture CreateVideoCapture(int deviceIndex)
        {
            return new VideoCapture(deviceIndex);
        }

        private static bool ValidateCapture(VideoCapture capture)
        {
            return capture.IsOpened();
        }

        private void CleanupAfterFailedInitialization()
        {
            lock (_lockObject)
            {
                CleanupCapture();
                _currentDevice = null;
            }
        }

        private void ValidateCaptureReady()
        {
            if (_capture == null || !_capture.IsOpened())
            {
                throw new InvalidOperationException("Cannot start capture: device not initialized");
            }
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
                TryReleaseCapture(_capture);
                TryDispose(_capture, "capture");
                _capture = null;
            }
        }

        private static void TryReleaseCapture(VideoCapture capture)
        {
            try
            {
                capture.Release();
            }
            catch (Exception ex)
            {
                Log($"Warning releasing capture: {ex.GetType().Name}");
            }
        }

        private static void TryDispose(IDisposable resource, string resourceName)
        {
            try
            {
                resource.Dispose();
            }
            catch (Exception ex)
            {
                Log($"Warning disposing {resourceName}: {ex.GetType().Name}");
            }
        }

        #endregion

        #region Utility Methods

        private static int ExtractDeviceIndex(string deviceName)
        {
            return int.TryParse(deviceName.Split(' ').LastOrDefault(), out int index) ? index : 0;
        }

        private static string GetDeviceName(int index) => $"Camera {index}";

        private static (double width, double height) GetResolution(VideoCapture capture)
        {
            return (
                capture.Get(VideoCaptureProperties.FrameWidth),
                capture.Get(VideoCaptureProperties.FrameHeight)
            );
        }

        #endregion

        #region Logging

        private static void Log(string message)
        {
            Debug.WriteLine($"[CameraOpenCvService] {message}");
        }

        private static void LogError(string operation, Exception ex)
        {
            Debug.WriteLine($"[CameraOpenCvService] ✗ Error {operation}: {ex.GetType().Name} - {ex.Message}");
        }

        private static void LogDeviceInfo(VideoCapture capture, string deviceName)
        {
            var (width, height) = GetResolution(capture);
            var fps = capture.Get(VideoCaptureProperties.Fps);

            Log($"✓ Successfully initialized device: {deviceName}");
            Log($"  Resolution: {width}x{height}");
            Log($"  FPS: {fps}\n");
        }

        private static void PrintEnumerationHeader()
        {
            Debug.WriteLine("\n" + new string('=', 70));
            Debug.WriteLine("[CameraOpenCvService] CAMERA DEVICE ENUMERATION STARTED");
            Debug.WriteLine(new string('=', 70));
            Debug.WriteLine($"[CameraOpenCvService] Environment: {Environment.OSVersion}");
            Debug.WriteLine($"[CameraOpenCvService] .NET Version: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            Debug.WriteLine($"[CameraOpenCvService] OpenCV Backend: Checking available...\n");
        }

        private static void PrintEnumerationFooter(List<string> devices)
        {
            Debug.WriteLine("\n" + new string('-', 70));
            Debug.WriteLine($"[CameraOpenCvService] ENUMERATION COMPLETE: Found {devices.Count} device(s)");
            Debug.WriteLine(new string('=', 70) + "\n");

            if (devices.Count == 0)
            {
                PrintTroubleshootingInfo();
            }
            else
            {
                foreach (var device in devices)
                {
                    Log($"✓ Available: {device}");
                }
            }
        }

        private static void PrintTroubleshootingInfo()
        {
            Debug.WriteLine("\n" + new string('!', 70));
            Debug.WriteLine("[CameraOpenCvService] ⚠ NO CAMERA DEVICES DETECTED");
            Debug.WriteLine(new string('!', 70));
            Debug.WriteLine("\nTROUBLESHOOTING STEPS:");
            Debug.WriteLine("\n1. VERIFY CAMERA HARDWARE:");
            Debug.WriteLine("   - Check physical connection and USB port");
            Debug.WriteLine("   - Verify camera indicator light is on");
            Debug.WriteLine("\n2. CHECK WINDOWS DEVICE MANAGER:");
            Debug.WriteLine("   - Win + X → Device Manager → Cameras");
            Debug.WriteLine("   - Right-click camera → Properties");
            Debug.WriteLine("   - Update driver if needed");
            Debug.WriteLine("\n3. CLOSE BLOCKING APPLICATIONS:");
            Debug.WriteLine("   - Zoom, Teams, Skype, Discord, OBS");
            Debug.WriteLine("   - Browser tabs using webcam");
            Debug.WriteLine("\n4. CHECK WINDOWS CAMERA PRIVACY:");
            Debug.WriteLine("   - Settings → Privacy → Camera");
            Debug.WriteLine("   - Enable camera access for this app");
            Debug.WriteLine("\n5. TEST WITH WINDOWS CAMERA APP:");
            Debug.WriteLine("   - If Camera app fails → system-level issue");
            Debug.WriteLine(new string('!', 70) + "\n");
        }

        #endregion
    }
}