using LaptopHealth.Services.Interfaces;

namespace LaptopHealth.Services.Infrastructure
{
    /// <summary>
    /// Infrastructure-level camera service
    /// Coordinates between UI and hardware service
    /// Manages state and business logic
    /// </summary>
    public class CameraService : ICameraService
    {
        private readonly ICameraHardwareService _hardwareService;
        private string? _selectedDevice;
        private string _lastAction = "None";

        public string? SelectedDevice => _selectedDevice;
        public bool IsCameraRunning => _hardwareService.IsCapturing;
        public string LastAction => _lastAction;

        public CameraService(ICameraHardwareService hardwareService)
        {
            _hardwareService = hardwareService ?? throw new ArgumentNullException(nameof(hardwareService));
        }

        public async Task<IEnumerable<string>> GetAvailableDevicesAsync()
        {
            System.Diagnostics.Debug.WriteLine("[CameraService] Fetching available devices");
            _lastAction = "Fetched available devices";
            
            return await _hardwareService.GetAvailableDevicesAsync();
        }

        public async Task<bool> SelectDeviceAsync(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                System.Diagnostics.Debug.WriteLine("[CameraService] Invalid device name");
                _lastAction = "Failed to select device: Invalid name";
                return false;
            }

            // Stop current camera if running
            if (IsCameraRunning)
            {
                await StopCameraAsync();
            }

            System.Diagnostics.Debug.WriteLine($"[CameraService] Selecting device: {deviceName}");
            
            var result = await _hardwareService.InitializeDeviceAsync(deviceName);
            
            if (result)
            {
                _selectedDevice = deviceName;
                _lastAction = $"Selected device: {deviceName}";
            }
            else
            {
                _lastAction = $"Failed to select device: {deviceName}";
            }

            return result;
        }

        public async Task<bool> StartCameraAsync()
        {
            if (_selectedDevice == null)
            {
                System.Diagnostics.Debug.WriteLine("[CameraService] No device selected");
                _lastAction = "Failed to start: No device selected";
                return false;
            }

            if (IsCameraRunning)
            {
                System.Diagnostics.Debug.WriteLine("[CameraService] Camera already running");
                _lastAction = "Camera already running";
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"[CameraService] Starting camera: {_selectedDevice}");
            
            var result = await _hardwareService.StartCaptureAsync();
            
            _lastAction = result ? $"Started camera: {_selectedDevice}" : "Failed to start camera";
            
            return result;
        }

        public async Task<bool> StopCameraAsync()
        {
            if (!IsCameraRunning)
            {
                System.Diagnostics.Debug.WriteLine("[CameraService] Camera not running");
                _lastAction = "Camera not running";
                return true;
            }

            System.Diagnostics.Debug.WriteLine($"[CameraService] Stopping camera");
            
            var result = await _hardwareService.StopCaptureAsync();
            
            _lastAction = result ? "Stopped camera" : "Failed to stop camera";
            
            return result;
        }

        public async Task<byte[]?> GetCurrentFrameAsync()
        {
            if (!IsCameraRunning)
            {
                return null;
            }

            return await _hardwareService.GetCurrentFrameAsync();
        }
    }
}
