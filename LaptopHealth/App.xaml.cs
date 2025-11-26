using LaptopHealth.Configuration;
using LaptopHealth.Infrastructure;
using LaptopHealth.Services.Infrastructure;
using LaptopHealth.Services.Interfaces;
using LaptopHealth.Views;
using LaptopHealth.Views.Tests;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;
using IApplicationLogger = LaptopHealth.Services.Interfaces.ILogger;

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
            var exceptionHandler = new GlobalExceptionHandler(ShutdownApplication);
            exceptionHandler.Register(this);

            var loadingWindow = new LoadingWindow();
            loadingWindow.Show();

            _ = InitializeApplicationAsync(loadingWindow);
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
                LoggingConfiguration.ConfigureSerilog(environment.IsProduction);

                ServiceProvider = BuildServiceProvider();

                var logger = ServiceProvider.GetRequiredService<IApplicationLogger>();
                logger.Troubleshoot($"APPLICATION STARTED - Logging initialized (Environment: {environment.Name})");

                // Set cancellation token for camera service
                var cameraService = ServiceProvider.GetService<ICameraService>();
                cameraService?.SetCancellationToken(_applicationCts!.Token);

                // Set cancellation token for audio service
                var audioService = ServiceProvider.GetService<IAudioService>();
                audioService?.SetCancellationToken(_applicationCts!.Token);

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

            services.AddApplicationServices();

            return services.BuildServiceProvider();
        }

        private void ShowMainWindow(LoadingWindow loadingWindow)
        {
            Dispatcher.Invoke(() =>
            {
                var mainWindow = ServiceProvider!.GetRequiredService<MainWindow>();
                mainWindow.Show();
                loadingWindow.Close();
            });
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

            TestRegistry.Register<MicrophoneTestPage>(
                "Microphone Test",
                "Tests microphone device enumeration and real-time frequency visualization"
            );

            TestRegistry.Register<AudioTestPage>(
                "Audio Test",
                "Simple audio test page demonstrating MVVM structure with IAudioService"
            );

            TestRegistry.Register<KeyboardTestPage>(
                "Keyboard Test",
                "Tests keyboard keys and displays pressed keys"
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
