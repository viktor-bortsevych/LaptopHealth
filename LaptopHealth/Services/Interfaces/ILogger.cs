namespace LaptopHealth.Services.Interfaces
{
    /// <summary>
    /// Abstraction for logging throughout the application
    /// Supports structured logging with different severity levels
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs informational messages
        /// </summary>
        void Info(string message);

        /// <summary>
        /// Logs warning messages
        /// </summary>
        void Warn(string message);

        /// <summary>
        /// Logs error messages with exception information
        /// </summary>
        void Error(string message, Exception? ex = null);

        /// <summary>
        /// Logs debug messages (only in debug builds)
        /// </summary>
        void Debug(string message);

        /// <summary>
        /// Logs a formatted section header
        /// </summary>
        void Section(string title);

        /// <summary>
        /// Logs a formatted section footer
        /// </summary>
        void SectionEnd();

        /// <summary>
        /// Logs troubleshooting information
        /// </summary>
        void Troubleshoot(string message);
    }
}
