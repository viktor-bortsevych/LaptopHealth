using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels;
using System.Windows.Controls;

namespace LaptopHealth.Views.Tests
{
    /// <summary>
    /// Interaction logic for MicrophoneTestPage.xaml
    /// </summary>
    public partial class MicrophoneTestPage : UserControl, ITestPage
    {
        private readonly MicrophoneTestPageViewModel _viewModel;

        public string TestName => "Microphone Test";
        public string TestDescription => "Tests microphone device enumeration and real-time frequency visualization";

        public MicrophoneTestPage(MicrophoneTestPageViewModel viewModel)
        {
            LogInfo("[MicrophoneTestPage] Constructor called");
            LogInfo($"[MicrophoneTestPage] Instance hash: {GetHashCode()}");

            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            LogInfo("[MicrophoneTestPage] DataContext set");
            LogInfo($"[MicrophoneTestPage] ViewModel type: {_viewModel.GetType().Name}");
            LogInfo($"[MicrophoneTestPage] ViewModel hash: {_viewModel.GetHashCode()}");

            // Initialize when loaded
            Loaded += async (s, e) => 
            {
                LogInfo("[MicrophoneTestPage] Loaded event fired");
                await _viewModel.InitializeAsync();
            };

            LogInfo("[MicrophoneTestPage] Constructor completed");
        }

        /// <summary>
        /// Performs async cleanup before page unload. Called by MainWindow before navigation.
        /// </summary>
        public async Task CleanupAsync()
        {
            LogInfo("[MicrophoneTestPage] ===============================================================================");
            LogInfo("[MicrophoneTestPage] CleanupAsync called");
            LogInfo($"[MicrophoneTestPage] Instance hash: {GetHashCode()}");
            LogInfo($"[MicrophoneTestPage] ViewModel hash: {_viewModel.GetHashCode()}");

            try
            {
                LogInfo("[MicrophoneTestPage] Calling ViewModel.CleanupAsync");
                await _viewModel.CleanupAsync();
                LogInfo("[MicrophoneTestPage] ViewModel.CleanupAsync completed");

                LogInfo("[MicrophoneTestPage] Calling ViewModel.Dispose");
                _viewModel.Dispose();
                LogInfo("[MicrophoneTestPage] ViewModel.Dispose completed");

                LogInfo("[MicrophoneTestPage] CleanupAsync completed successfully");
            }
            catch (Exception ex)
            {
                LogError("[MicrophoneTestPage] Error during CleanupAsync", ex);
            }
            finally
            {
                LogInfo("[MicrophoneTestPage] ===============================================================================");
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
