using System.Configuration;
using System.Data;
using System.Windows;
using System.Threading.Tasks;
using LaptopHealth.Views;

namespace LaptopHealth
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Show loading window
            LoadingWindow loadingWindow = new LoadingWindow();
            loadingWindow.Show();

            Task.Run(async () =>
            {
                Dispatcher.Invoke(() =>
                {
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                    loadingWindow.Close();
                });
            });
        }
    }
}
