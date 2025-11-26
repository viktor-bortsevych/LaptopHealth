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

        public Task<IEnumerable<string>> GetAvailableDevicesAsync()
        {
            logger.Info("Fetching available microphone devices");
            _lastAction = "Fetched available devices";
            return Task.FromResult(hardwareService.GetAvailableDevices());
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

        public Task<bool> StartMicrophoneAsync()
        {
            if (_selectedDevice == null)
            {
                logger.Warn("No microphone device selected");
                _lastAction = "Failed to start: No device selected";
                return Task.FromResult(false);
            }

            if (IsMicrophoneRunning)
            {
                logger.Info("Microphone already running");
                _lastAction = "Microphone already running";
                return Task.FromResult(true);
            }

            logger.Info($"Starting microphone: {_selectedDevice}");

            var result = hardwareService.StartCapture();

            _lastAction = result ? $"Started microphone: {_selectedDevice}" : "Failed to start microphone";

            if (!result)
            {
                logger.Error("Failed to start microphone");
            }

            return Task.FromResult(result);
        }

        public Task<bool> StopMicrophoneAsync()
        {
            if (!IsMicrophoneRunning)
            {
                logger.Info("Microphone not running");
                _lastAction = "Microphone not running";
                return Task.FromResult(true);
            }

            logger.Info("Stopping microphone");

            var result = hardwareService.StopCapture();

            _lastAction = result ? "Stopped microphone" : "Failed to stop microphone";

            if (!result)
            {
                logger.Error("Failed to stop microphone");
            }

            return Task.FromResult(result);
        }

        public Task<float[]?> GetFrequencyDataAsync()
        {
            if (!IsMicrophoneRunning)
            {
                return Task.FromResult<float[]?>(null);
            }

            return Task.FromResult(hardwareService.GetFrequencyData());
        }
    }
}
