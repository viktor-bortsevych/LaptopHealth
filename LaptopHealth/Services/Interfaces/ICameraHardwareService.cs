namespace LaptopHealth.Services.Interfaces
{
    /// <summary>
    /// Hardware-level service for camera operations
    /// This service directly interacts with 3rd party libraries (e.g., OpenCV, DirectShow, etc.)
    /// Responsible for low-level camera hardware access
    /// </summary>
    public interface ICameraHardwareService
    {
        /// <summary>
        /// Gets a list of available camera devices
        /// </summary>
        /// <returns>List of camera device names</returns>
        Task<IEnumerable<string>> GetAvailableDevicesAsync();

        /// <summary>
        /// Initializes a specific camera device
        /// </summary>
        /// <param name="deviceName">Name of the camera device</param>
        /// <returns>True if initialization was successful</returns>
        Task<bool> InitializeDeviceAsync(string deviceName);

        /// <summary>
        /// Starts capturing video from the initialized camera
        /// </summary>
        /// <returns>True if camera started successfully</returns>
        Task<bool> StartCaptureAsync();

        /// <summary>
        /// Stops capturing video from the camera
        /// </summary>
        /// <returns>True if camera stopped successfully</returns>
        Task<bool> StopCaptureAsync();

        /// <summary>
        /// Gets the current frame from the camera
        /// </summary>
        /// <returns>Byte array representing the current frame (e.g., bitmap data)</returns>
        Task<byte[]?> GetCurrentFrameAsync();

        /// <summary>
        /// Releases camera resources
        /// </summary>
        Task DisposeAsync();

        /// <summary>
        /// Indicates whether the camera is currently capturing
        /// </summary>
        bool IsCapturing { get; }
    }
}
