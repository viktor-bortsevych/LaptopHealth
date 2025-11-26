using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels;
using System.Windows.Controls;

namespace LaptopHealth.Views.Tests
{
    public partial class AudioTestPage : UserControl, ITestPage
    {
        private readonly AudioTestPageViewModel _viewModel;

        public string TestName => "Audio Test";
        public string TestDescription => "";

        public AudioTestPage(AudioTestPageViewModel viewModel)
        {
            LogInfo("[AudioTestPage] Constructor called");
            LogInfo($"[AudioTestPage] Instance hash: {GetHashCode()}");

            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            LogInfo("[AudioTestPage] DataContext set");
            LogInfo($"[AudioTestPage] ViewModel type: {_viewModel.GetType().Name}");
            LogInfo($"[AudioTestPage] ViewModel hash: {_viewModel.GetHashCode()}");
            LogInfo("[AudioTestPage] Constructor completed");
        }

        public async Task CleanupAsync()
        {
            LogInfo("[AudioTestPage] ===============================================================================");
            LogInfo("[AudioTestPage] CleanupAsync called");
            LogInfo($"[AudioTestPage] Instance hash: {GetHashCode()}");
            LogInfo($"[AudioTestPage] ViewModel hash: {_viewModel.GetHashCode()}");

            try
            {
                LogInfo("[AudioTestPage] Calling ViewModel.DisposeAsync");
                await _viewModel.DisposeAsync();
                LogInfo("[AudioTestPage] ViewModel.DisposeAsync completed");

                LogInfo("[AudioTestPage] CleanupAsync completed successfully");
            }
            catch (Exception ex)
            {
                LogError("[AudioTestPage] Error during CleanupAsync", ex);
            }
            finally
            {
                LogInfo("[AudioTestPage] ===============================================================================");
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