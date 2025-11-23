using LaptopHealth.Services.Interfaces;
using Serilog;
using IApplicationLogger = LaptopHealth.Services.Interfaces.ILogger;

namespace LaptopHealth.Services.Infrastructure
{
    /// <summary>
    /// Logger factory for creating Serilog-based loggers
    /// Used for backward compatibility or creating loggers outside of DI
    /// </summary>
    public static class LoggerFactory
    {
        /// <summary>
        /// Creates a logger instance using Serilog
        /// Assumes Serilog is already configured globally via Log.Logger
        /// </summary>
        public static IApplicationLogger CreateLogger<T>()
        {
            var serilogLogger = Log.ForContext<T>();
            return new SerilogLogger(serilogLogger);
        }

        /// <summary>
        /// Creates a logger instance using Serilog with a specific context name
        /// </summary>
        public static IApplicationLogger CreateLogger(string contextName)
        {
            var serilogLogger = Log.ForContext("Context", contextName);
            return new SerilogLogger(serilogLogger);
        }
    }
}
