using LaptopHealth.Services.Interfaces;
using Serilog;
using IApplicationLogger = LaptopHealth.Services.Interfaces.ILogger;

namespace LaptopHealth.Services.Infrastructure
{
    /// <summary>
    /// Serilog-based logger implementation
    /// Wraps Serilog ILogger to implement the application's ILogger interface
    /// </summary>
    public class SerilogLogger(global::Serilog.ILogger logger) : IApplicationLogger, IDisposable
    {
        private readonly global::Serilog.ILogger _log = logger ?? throw new ArgumentNullException(nameof(logger));
        private bool _disposed;

        public void Info(string message) => _log.Information(message);

        public void Warn(string message) => _log.Warning(message);

        public void Error(string message, Exception? ex = null)
        {
            if (ex != null)
                _log.Error(ex, message);
            else
                _log.Error(message);
        }

        public void Debug(string message) => _log.Debug(message);

        public void Section(string title)
        {
            _log.Debug("{SectionHeader}", new string('=', 70));
            _log.Debug("{SectionTitle}", title);
            _log.Debug("{SectionFooter}", new string('=', 70));
        }

        public void SectionEnd() => _log.Information("{SectionEnd}", new string('-', 70));

        public void Troubleshoot(string message)
        {
            _log.Debug("{TroubleshootMarker}", new string('!', 70));
            _log.Debug("{TroubleshootMessage}", message);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing && _log is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _disposed = true;
        }
    }
}
