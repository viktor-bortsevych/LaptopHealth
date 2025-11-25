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
        IEnumerable<string> GetAvailableDevices();

        /// <summary>
        /// Initializes a specific microphone device
        /// </summary>
        Task<bool> InitializeDeviceAsync(string deviceName, CancellationToken cancellationToken);

        /// <summary>
        /// Starts audio capture from the selected device
        /// </summary>
        bool StartCapture();

        /// <summary>
        /// Stops audio capture
        /// </summary>
        bool StopCapture();

        /// <summary>
        /// Gets the current frequency analysis data (32 frequency bands)
        /// </summary>
        float[]? GetFrequencyData();

        /// <summary>
        /// Indicates whether audio is currently being captured
        /// </summary>
        bool IsCapturing { get; }
    }
}
