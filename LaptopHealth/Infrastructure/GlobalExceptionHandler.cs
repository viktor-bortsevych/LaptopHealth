using LaptopHealth.Services.Infrastructure;
using System.Windows;
using System.Windows.Threading;

namespace LaptopHealth.Infrastructure
{
    /// <summary>
    /// Handles global unhandled exceptions across the application
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the GlobalExceptionHandler
    /// </remarks>
    /// <param name="shutdownCallback">Callback to invoke for application shutdown</param>
    public class GlobalExceptionHandler(Action<int> shutdownCallback)
    {
        private readonly Action<int> _shutdownCallback = shutdownCallback ?? throw new ArgumentNullException(nameof(shutdownCallback));

        /// <summary>
        /// Registers exception handlers for the application
        /// </summary>
        /// <param name="app">The application instance</param>
        public void Register(Application app)
        {
            ArgumentNullException.ThrowIfNull(app);

            // Handle exceptions on UI thread
            app.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Handle exceptions on background threads
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Handle task scheduler exceptions
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        /// <summary>
        /// Handles unhandled exceptions on the UI thread
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Ignore cancellation exceptions - they're expected during shutdown
            if (IsCancellationException(e.Exception))
            {
                e.Handled = true;
                return;
            }

            LogUnhandledException("UI Thread", e.Exception);
            e.Handled = true;
            _shutdownCallback(1);
        }

        /// <summary>
        /// Handles unhandled exceptions on background threads
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception ?? new Exception("Unknown exception occurred");

            if (IsCancellationException(exception))
            {
                return;
            }

            LogUnhandledException("Background Thread", exception);
            _shutdownCallback(1);
        }

        /// <summary>
        /// Handles unobserved task exceptions
        /// </summary>
        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Ignore cancellation exceptions - they're expected during shutdown
            if (IsCancellationException(e.Exception))
            {
                e.SetObserved();
                return;
            }

            LogUnhandledException("Task Scheduler", e.Exception);
            e.SetObserved();
            _shutdownCallback(1);
        }

        /// <summary>
        /// Checks if an exception is a cancellation exception that should be ignored
        /// </summary>
        private static bool IsCancellationException(Exception exception)
        {
            if (exception is OperationCanceledException or TaskCanceledException)
            {
                return true;
            }

            if (exception is AggregateException aggregateException)
            {
                return aggregateException.InnerExceptions.All(IsCancellationException);
            }

            return false;
        }

        /// <summary>
        /// Logs an unhandled exception using the logger factory
        /// </summary>
        private static void LogUnhandledException(string context, Exception exception)
        {
            try
            {
                var logger = LoggerFactory.CreateLogger<GlobalExceptionHandler>();
                logger.Error($"[CRITICAL] Unhandled exception on {context}", exception);
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] Unhandled exception on {context}: {exception}");
            }
        }
    }
}
