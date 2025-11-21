using LaptopHealth.Services.Infrastructure;
using LaptopHealth.Services.Interfaces;
using LaptopHealth.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace LaptopHealth
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Show loading window first
            LoadingWindow loadingWindow = new();
            loadingWindow.Show();

            // Run initialization on background thread
            Task.Run(async () =>
            {
                var services = new ServiceCollection();

                services.AddScoped<ICounterService, CounterService>();

                ServiceProvider = services.BuildServiceProvider();

                RegisterTestPages();

                // Show StylesPreviewWindow to demonstrate the design system
                Dispatcher.Invoke(() =>
                {
                    StylesPreviewWindow previewWindow = new();
                    previewWindow.Show();
                    loadingWindow.Close();
                });

            });
        }

        /// <summary>
        /// Registers all available test pages
        /// </summary>
        private static void RegisterTestPages()
        {
            TestRegistry.Register<CounterTestPage>(
                "Counter Test",
                "Tests counting functionality"
            );
        }
    }
}
