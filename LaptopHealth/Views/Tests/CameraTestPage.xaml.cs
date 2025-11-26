using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels;
using System.Windows.Controls;

namespace LaptopHealth.Views
{
    /// <summary>
    /// Interaction logic for CameraTestPage.xaml
    /// </summary>
    public partial class CameraTestPage : UserControl, ITestPage
    {
        private readonly CameraTestPageViewModel _viewModel;

        public string TestName => "Camera Test";
        public string TestDescription => "Tests camera device enumeration and control";

        public CameraTestPage(CameraTestPageViewModel viewModel)
        {
            LogInfo("[CameraTestPage] Constructor called");
            LogInfo($"[CameraTestPage] Instance hash: {GetHashCode()}");

            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            LogInfo("[CameraTestPage] DataContext set");
            LogInfo($"[CameraTestPage] ViewModel type: {_viewModel.GetType().Name}");
            LogInfo($"[CameraTestPage] ViewModel hash: {_viewModel.GetHashCode()}");

            // Initialize when loaded
            Loaded += async (s, e) =>
            {
                LogInfo("[CameraTestPage] Loaded event fired");
                await _viewModel.InitializeAsync();
            };

            LogInfo("[CameraTestPage] Constructor completed");
        }

        /// <summary>
        /// Performs async cleanup before page unload. Called by MainWindow before navigation.
        /// </summary>
        public async Task CleanupAsync()
        {
            LogInfo("[CameraTestPage] ===============================================================================");
            LogInfo("[CameraTestPage] CleanupAsync called");
            LogInfo($"[CameraTestPage] Instance hash: {GetHashCode()}");
            LogInfo($"[CameraTestPage] ViewModel hash: {_viewModel.GetHashCode()}");

            try
            {
                LogInfo("[CameraTestPage] Calling ViewModel.CleanupAsync");
                await _viewModel.CleanupAsync();
                LogInfo("[CameraTestPage] ViewModel.CleanupAsync completed");

                LogInfo("[CameraTestPage] Calling ViewModel.Dispose");
                _viewModel.Dispose();
                LogInfo("[CameraTestPage] ViewModel.Dispose completed");

                LogInfo("[CameraTestPage] CleanupAsync completed successfully");
            }
            catch (Exception ex)
            {
                LogError("[CameraTestPage] Error during CleanupAsync", ex);
            }
            finally
            {
                LogInfo("[CameraTestPage] ===============================================================================");
            }
        }

        #region Logging

        private static void LogInfo(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }

        private static void LogError(string message, Exception ex)
        {
            string fullMessage = $"{message}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(fullMessage);
        }

        #endregion
    }
}