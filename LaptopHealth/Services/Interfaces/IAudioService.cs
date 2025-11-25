namespace LaptopHealth.Services.Interfaces
{
    /// <summary>
    /// Infrastructure-level service for audio/microphone operations
    /// Acts as a bridge between UI and hardware service
    /// Handles business logic, state management, and coordinates audio operations
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// Gets a list of available microphone devices
        /// </summary>
        /// <returns>List of microphone device names</returns>
        Task<IEnumerable<string>> GetAvailableDevicesAsync();

        /// <summary>
        /// Selects and initializes a microphone device
        /// </summary>
        /// <param name="deviceName">Name of the device to select</param>
        /// <returns>True if selection was successful</returns>
        Task<bool> SelectDeviceAsync(string deviceName);

        /// <summary>
        /// Starts the microphone capture
        /// </summary>
        /// <returns>True if microphone started successfully</returns>
        Task<bool> StartMicrophoneAsync();

        /// <summary>
        /// Stops the microphone capture
        /// </summary>
        /// <returns>True if microphone stopped successfully</returns>
        Task<bool> StopMicrophoneAsync();

        /// <summary>
        /// Gets the current frequency band data for visualization (32 bands)
        /// </summary>
        /// <returns>Array of frequency values (0-10 scale) for each band</returns>
        Task<float[]?> GetFrequencyDataAsync();

        /// <summary>
        /// Gets the currently selected device name
        /// </summary>
        string? SelectedDevice { get; }

        /// <summary>
        /// Indicates whether the microphone is currently running
        /// </summary>
        bool IsMicrophoneRunning { get; }

        /// <summary>
        /// Gets the last action performed
        /// </summary>
        string LastAction { get; }

        /// <summary>
        /// Sets the application-wide cancellation token for graceful shutdown support
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to use for operations</param>
        void SetCancellationToken(CancellationToken cancellationToken);
    }
}