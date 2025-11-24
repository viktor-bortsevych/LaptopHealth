namespace LaptopHealth.Services.Interfaces
{
    /// <summary>
    /// Hardware-level audio service interface
    /// Handles low-level microphone operations and audio signal processing
    /// </summary>
    public interface IAudioHardwareService
    {
        /// <summary>
        /// Gets a list of available microphone devices
        /// </summary>
        Task<IEnumerable<string>> GetAvailableDevicesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Initializes a specific microphone device
        /// </summary>
        Task<bool> InitializeDeviceAsync(string deviceName, CancellationToken cancellationToken);

        /// <summary>
        /// Starts audio capture from the selected device
        /// </summary>
        Task<bool> StartCaptureAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stops audio capture
        /// </summary>
        Task<bool> StopCaptureAsync();

        /// <summary>
        /// Gets the current frequency analysis data (32 frequency bands)
        /// </summary>
        Task<float[]?> GetFrequencyDataAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Indicates whether audio is currently being captured
        /// </summary>
        bool IsCapturing { get; }
    }
}
