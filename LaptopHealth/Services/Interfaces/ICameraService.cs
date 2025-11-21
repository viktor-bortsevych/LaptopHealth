namespace LaptopHealth.Services.Interfaces
{
    /// <summary>
    /// Infrastructure-level service for camera operations
    /// Acts as a bridge between UI and hardware service
    /// Handles business logic, state management, and coordinates camera operations
    /// </summary>
    public interface ICameraService
    {
        /// <summary>
        /// Gets a list of available camera devices
        /// </summary>
        /// <returns>List of camera device names</returns>
        Task<IEnumerable<string>> GetAvailableDevicesAsync();

        /// <summary>
        /// Selects and initializes a camera device
        /// </summary>
        /// <param name="deviceName">Name of the device to select</param>
        /// <returns>True if selection was successful</returns>
        Task<bool> SelectDeviceAsync(string deviceName);

        /// <summary>
        /// Starts the camera capture
        /// </summary>
        /// <returns>True if camera started successfully</returns>
        Task<bool> StartCameraAsync();

        /// <summary>
        /// Stops the camera capture
        /// </summary>
        /// <returns>True if camera stopped successfully</returns>
        Task<bool> StopCameraAsync();

        /// <summary>
        /// Gets the current camera frame for display
        /// </summary>
        /// <returns>Image data for UI rendering</returns>
        Task<byte[]?> GetCurrentFrameAsync();

        /// <summary>
        /// Gets the currently selected device name
        /// </summary>
        string? SelectedDevice { get; }

        /// <summary>
        /// Indicates whether the camera is currently running
        /// </summary>
        bool IsCameraRunning { get; }

        /// <summary>
        /// Gets the last action performed
        /// </summary>
        string LastAction { get; }
    }
}
