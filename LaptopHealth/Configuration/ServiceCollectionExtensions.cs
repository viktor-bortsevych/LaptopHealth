using LaptopHealth.Services.Hardware;
using LaptopHealth.Services.Infrastructure;
using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels;
using LaptopHealth.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IApplicationLogger = LaptopHealth.Services.Interfaces.ILogger;

namespace LaptopHealth.Configuration
{
    /// <summary>
    /// Extension methods for configuring application services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all application services
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services
                .AddLogging()
                .AddHardwareServices()
                .AddViewModels()
                .AddViews();

            return services;
        }

        /// <summary>
        /// Registers logging services
        /// </summary>
        private static IServiceCollection AddLogging(this IServiceCollection services)
        {
            services.AddSingleton<IApplicationLogger>(provider =>
            {
                var serilogLogger = Log.ForContext<App>();
                return new SerilogLogger(serilogLogger);
            });

            return services;
        }

        /// <summary>
        /// Registers hardware-related services
        /// </summary>
        private static IServiceCollection AddHardwareServices(this IServiceCollection services)
        {
            services.AddScoped<ICameraHardwareService, CameraOpenCvService>();
            services.AddScoped<ICameraService, CameraService>();

            return services;
        }

        /// <summary>
        /// Registers all ViewModels
        /// </summary>
        private static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<CameraTestPageViewModel>();

            return services;
        }

        /// <summary>
        /// Registers all Views
        /// </summary>
        private static IServiceCollection AddViews(this IServiceCollection services)
        {
            services.AddTransient<MainWindow>();
            services.AddTransient<CameraTestPage>();

            return services;
        }
    }
}
