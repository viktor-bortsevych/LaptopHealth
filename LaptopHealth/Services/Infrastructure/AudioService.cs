using LaptopHealth.Services.Interfaces;

namespace LaptopHealth.Services.Infrastructure
{
    /// <summary>
    /// Infrastructure-level audio service
    /// Coordinates between UI and hardware service
    /// Manages state and business logic
    /// </summary>
    public class AudioService(IAudioHardwareService hardwareService, ILogger logger) : IAudioService
    {
        private string? _selectedDevice;
        private string _lastAction = "None";
        private CancellationToken _cancellationToken = CancellationToken.None;

        public string? SelectedDevice => _selectedDevice;
        public bool IsMicrophoneRunning => hardwareService.IsCapturing;
        public string LastAction => _lastAction;

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public async Task<IEnumerable<string>> GetAvailableDevicesAsync()
        {
            logger.Info("Fetching available microphone devices");
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

            // Stop current microphone if running
            if (IsMicrophoneRunning)
            {
                await StopMicrophoneAsync();
            }

            logger.Info($"Selecting microphone device: {deviceName}");

            var result = await hardwareService.InitializeDeviceAsync(deviceName, _cancellationToken);

            if (result)
            {
                _selectedDevice = deviceName;
                _lastAction = $"Selected device: {deviceName}";
                logger.Info($"Microphone device selected successfully: {deviceName}");
            }
            else
            {
                _lastAction = $"Failed to select device: {deviceName}";
                logger.Error($"Failed to select microphone device: {deviceName}");
            }

            return result;
        }

        public async Task<bool> StartMicrophoneAsync()
        {
            if (_selectedDevice == null)
            {
                logger.Warn("No microphone device selected");
                _lastAction = "Failed to start: No device selected";
                return false;
            }

            if (IsMicrophoneRunning)
            {
                logger.Info("Microphone already running");
                _lastAction = "Microphone already running";
                return true;
            }

            logger.Info($"Starting microphone: {_selectedDevice}");

            var result = await hardwareService.StartCaptureAsync(_cancellationToken);

            _lastAction = result ? $"Started microphone: {_selectedDevice}" : "Failed to start microphone";

            if (!result)
            {
                logger.Error("Failed to start microphone");
            }

            return result;
        }

        public async Task<bool> StopMicrophoneAsync()
        {
            if (!IsMicrophoneRunning)
            {
                logger.Info("Microphone not running");
                _lastAction = "Microphone not running";
                return true;
            }

            logger.Info("Stopping microphone");

            var result = await hardwareService.StopCaptureAsync();

            _lastAction = result ? "Stopped microphone" : "Failed to stop microphone";

            if (!result)
            {
                logger.Error("Failed to stop microphone");
            }

            return result;
        }

        public async Task<float[]?> GetFrequencyDataAsync()
        {
            if (!IsMicrophoneRunning)
            {
                return null;
            }

            return await hardwareService.GetFrequencyDataAsync(_cancellationToken);
        }
    }
}
