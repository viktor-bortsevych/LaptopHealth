using Serilog;
using System.IO;

namespace LaptopHealth.Configuration
{
    /// <summary>
    /// Configuration for application logging
    /// </summary>
    public static class LoggingConfiguration
    {
        /// <summary>
        /// Configures Serilog based on the environment
        /// </summary>
        /// <param name="isProduction">Whether the application is running in production mode</param>
        public static void ConfigureSerilog(bool isProduction)
        {
            var logConfig = new LoggerConfiguration()
                .Enrich.FromLogContext();

            if (isProduction)
            {
                ConfigureProductionLogging(logConfig);
            }
            else
            {
                ConfigureDevelopmentLogging(logConfig);
            }

            Log.Logger = logConfig.CreateLogger();
        }

        /// <summary>
        /// Configures logging for production environment (file-based)
        /// </summary>
        private static void ConfigureProductionLogging(LoggerConfiguration logConfig)
        {
            logConfig.MinimumLevel.Information();

            var logPath = CreateLogFilePath();
            logConfig.WriteTo.File(
                logPath,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7
            );
        }

        /// <summary>
        /// Configures logging for development environment (console-based)
        /// </summary>
        private static void ConfigureDevelopmentLogging(LoggerConfiguration logConfig)
        {
            logConfig.MinimumLevel.Debug();

            logConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            );
        }

        /// <summary>
        /// Creates the log file path for production logging
        /// </summary>
        private static string CreateLogFilePath()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var logDir = Path.Combine(exeDir, "Logs");
            Directory.CreateDirectory(logDir);
            return Path.Combine(logDir, "LaptopHealth_.log");
        }
    }
}
