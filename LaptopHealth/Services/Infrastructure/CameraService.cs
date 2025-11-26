using LaptopHealth.Services.Interfaces;

namespace LaptopHealth.Services.Infrastructure
{
    /// <summary>
    /// Infrastructure-level camera service
    /// Coordinates between UI and hardware service
    /// Manages state and business logic
    /// </summary>
    public class CameraService(ICameraHardwareService hardwareService, ILogger logger) : ICameraService
    {
        private string? _selectedDevice;
        private string _lastAction = "None";
        private CancellationToken _cancellationToken = CancellationToken.None;

        public string? SelectedDevice => _selectedDevice;
        public bool IsCameraRunning => hardwareService.IsCapturing;
        public string LastAction => _lastAction;

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public async Task<IEnumerable<string>> GetAvailableDevicesAsync()
        {
            logger.Info("Fetching available devices");
            _lastAction = "Fetched available devices";

            return await hardwareService.GetAvailableDevicesAsync(_cancellationToken);
        }

        public async Task<bool> SelectDeviceAsync(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                logger.Warn("Invalid device name");
                _lastAction = "Failed to select device: Invalid name";
                return false;
            }

            // Stop current camera if running
            if (IsCameraRunning)
            {
                await StopCameraAsync();
            }

            logger.Info($"Selecting device: {deviceName}");

            var result = await hardwareService.InitializeDeviceAsync(deviceName, _cancellationToken);

            if (result)
            {
                _selectedDevice = deviceName;
                _lastAction = $"Selected device: {deviceName}";
                logger.Info($"Device selected successfully: {deviceName}");
            }
            else
            {
                _lastAction = $"Failed to select device: {deviceName}";
                logger.Error($"Failed to select device: {deviceName}");
            }

            return result;
        }

        public async Task<bool> StartCameraAsync()
        {
            if (_selectedDevice == null)
            {
                logger.Warn("No device selected");
                _lastAction = "Failed to start: No device selected";
                return false;
            }

            if (IsCameraRunning)
            {
                logger.Info("Camera already running");
                _lastAction = "Camera already running";
                return true;
            }

            logger.Info($"Starting camera: {_selectedDevice}");

            var result = await hardwareService.StartCaptureAsync(_cancellationToken);

            _lastAction = result ? $"Started camera: {_selectedDevice}" : "Failed to start camera";

            if (!result)
            {
                logger.Error("Failed to start camera");
            }

            return result;
        }

        public async Task<bool> StopCameraAsync()
        {
            if (!IsCameraRunning)
            {
                logger.Info("Camera not running");
                _lastAction = "Camera not running";
                return true;
            }

            logger.Info("Stopping camera");

            var result = await hardwareService.StopCaptureAsync();

            _lastAction = result ? "Stopped camera" : "Failed to stop camera";

            if (!result)
            {
                logger.Error("Failed to stop camera");
            }

            return result;
        }

        public async Task<byte[]?> GetCurrentFrameAsync()
        {
            if (!IsCameraRunning)
            {
                return null;
            }

            return await hardwareService.GetCurrentFrameAsync(_cancellationToken);
        }
    }
}
