using LaptopHealth.Services.Hardware;
using LaptopHealth.Services.Infrastructure;
using LaptopHealth.Services.Interfaces;
using LaptopHealth.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;
using System.IO;
using IApplicationLogger = LaptopHealth.Services.Interfaces.ILogger;
using System.Threading;

namespace LaptopHealth
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static ServiceProvider? ServiceProvider { get; private set; }
        private bool _isShuttingDown;
        private CancellationTokenSource? _applicationCts;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create application-wide cancellation token
            _applicationCts = new CancellationTokenSource();

            // Register global exception handlers
            RegisterExceptionHandlers();

            var loadingWindow = new LoadingWindow();
            loadingWindow.Show();

            _ = InitializeApplicationAsync(loadingWindow);
        }

        /// <summary>
        /// Registers global exception handlers for both UI and background threads
        /// </summary>
        private void RegisterExceptionHandlers()
        {
            // Handle exceptions on UI thread
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Handle exceptions on background threads
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Handle task scheduler exceptions
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        /// <summary>
        /// Handles unhandled exceptions on the UI thread
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Ignore cancellation exceptions - they're expected during shutdown
            if (IsCancellationException(e.Exception))
            {
                e.Handled = true;
                return;
            }

            LogUnhandledException("UI Thread", e.Exception);
            e.Handled = true;
            ShutdownApplication(1);
        }

        /// <summary>
        /// Handles unhandled exceptions on background threads
        /// </summary>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception ?? new Exception("Unknown exception occurred");
            
            if (IsCancellationException(exception))
            {
                return;
            }

            LogUnhandledException("Background Thread", exception);
            ShutdownApplication(1);
        }

        /// <summary>
        /// Handles unobserved task exceptions
        /// </summary>
        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Ignore cancellation exceptions - they're expected during shutdown
            if (IsCancellationException(e.Exception))
            {
                e.SetObserved();
                return;
            }

            LogUnhandledException("Task Scheduler", e.Exception);
            e.SetObserved();
            ShutdownApplication(1);
        }

        /// <summary>
        /// Checks if an exception is a cancellation exception that should be ignored
        /// </summary>
        private static bool IsCancellationException(Exception exception)
        {
            // Check for direct cancellation exceptions
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
                var logger = LoggerFactory.CreateLogger<App>();
                logger.Error($"[CRITICAL] Unhandled exception on {context}", exception);
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] Unhandled exception on {context}: {exception}");
            }
        }

        /// <summary>
        /// Safely shuts down the application
        /// </summary>
        private void ShutdownApplication(int exitCode)
        {
            if (_isShuttingDown)
                return;

            _isShuttingDown = true;

            try
            {
                CancelAllOperations();
                CloseAllWindows();
                DisposeResources();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
            }
            finally
            {
                Shutdown(exitCode);
            }
        }

        /// <summary>
        /// Cancels all ongoing background tasks
        /// </summary>
        private void CancelAllOperations()
        {
            try
            {
                if (_applicationCts is { IsCancellationRequested: false })
                {
                    _applicationCts.Cancel();
                    _applicationCts.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cancelling operations: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes all windows gracefully
        /// </summary>
        private void CloseAllWindows()
        {
            try
            {
                var windowsToClose = Windows.Cast<Window>().ToList();
                foreach (var window in windowsToClose)
                {
                    try
                    {
                        window.Dispatcher.Invoke(() =>
                        {
                            if (window.IsLoaded)
                                window.Close();
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error closing window {window.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing windows: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes all services and resources
        /// </summary>
        private static void DisposeResources()
        {
            try
            {
                if (ServiceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                    ServiceProvider = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing ServiceProvider: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the application-wide cancellation token for long-running operations
        /// </summary>
        public static CancellationToken GetApplicationCancellationToken()
        {
            var app = Current as App;
            return app?._applicationCts?.Token ?? CancellationToken.None;
        }

        private async Task InitializeApplicationAsync(LoadingWindow loadingWindow)
        {
            try
            {
                var environment = DetectEnvironment();
                ConfigureSerilog(environment.IsProduction);

                ServiceProvider = BuildServiceProvider();

                var logger = ServiceProvider.GetRequiredService<IApplicationLogger>();
                logger.Troubleshoot($"APPLICATION STARTED - Logging initialized (Environment: {environment.Name})");

                // Set cancellation token for camera service
                var cameraService = ServiceProvider.GetService<ICameraService>();
                cameraService?.SetCancellationToken(_applicationCts!.Token);

                RegisterTestPages();

                ShowMainWindow(loadingWindow);
            }
            catch (Exception ex)
            {
                LoggerFactory.CreateLogger<App>().Error("Failed to initialize application", ex);
                loadingWindow.Close();
                Shutdown(1);
            }
        }

        private static EnvironmentInfo DetectEnvironment()
        {
            var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            
            // If environment variable is not set, default to Production (use file logging)
            if (string.IsNullOrWhiteSpace(envName))
            {
                return new EnvironmentInfo { Name = "Production", IsProduction = true };
            }
            
            var isProduction = envName.Equals("Production", StringComparison.OrdinalIgnoreCase);
            return new EnvironmentInfo { Name = envName, IsProduction = isProduction };
        }

        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IApplicationLogger>(provider =>
            {
                var serilogLogger = Log.ForContext<App>();
                return new SerilogLogger(serilogLogger);
            });

            services.AddScoped<ICameraHardwareService, CameraOpenCvService>();
            services.AddScoped<ICameraService, CameraService>();

            return services.BuildServiceProvider();
        }

        private void ShowMainWindow(LoadingWindow loadingWindow)
        {
            Dispatcher.Invoke(() =>
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                loadingWindow.Close();
            });
        }

        /// <summary>
        /// Configures Serilog for the application
        /// </summary>
        private static void ConfigureSerilog(bool isProduction)
        {
            var logConfig = new LoggerConfiguration()
                .Enrich.FromLogContext();

            if (isProduction)
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
            else
            {
                logConfig.MinimumLevel.Debug();
                
                logConfig.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                );
            }

            Log.Logger = logConfig.CreateLogger();
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

        /// <summary>
        /// Registers all available test pages
        /// </summary>
        private static void RegisterTestPages()
        {
            TestRegistry.Register<CameraTestPage>(
                "Camera Test",
                "Tests camera device enumeration and control"
            );
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            try
            {
                LoggerFactory.CreateLogger<App>().Troubleshoot("APPLICATION SHUTTING DOWN");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private sealed record EnvironmentInfo
        {
            public string Name { get; set; } = string.Empty;
            public bool IsProduction { get; set; }
        }
    }
}
