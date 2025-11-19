using System.Configuration;
using System.Data;
using System.Windows;
using System.Threading.Tasks;

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

            // Simulate loading work (replace with your actual initialization)
            Task.Run(async () =>
            {
                // Add your initialization code here
                await Task.Delay(2000); // Example: 2 second delay

                // Show main window and close loading window
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
