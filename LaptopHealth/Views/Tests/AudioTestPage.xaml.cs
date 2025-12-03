using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LaptopHealth.Views.Tests
{
    public partial class AudioTestPage : UserControl, ITestPage, IKeyboardShortcutHandler
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

            // Subscribe to preview keyboard events as fallback for when this control has focus
            this.PreviewKeyDown += AudioTestPage_PreviewKeyDown;
        }

        private void AudioTestPage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleKeyDown(e.Key);
        }

        public bool HandleKeyDown(Key key)
        {
            switch (key)
            {
                case Key.Space:
                    if (_viewModel.PlayCommand.CanExecute(null))
                    {
                        _viewModel.PlayCommand.Execute(null);
                        return true;
                    }
                    break;
                case Key.Z:
                    if (_viewModel.SetBalanceLeftCommand.CanExecute(null))
                    {
                        _viewModel.SetBalanceLeftCommand.Execute(null);
                        return true;
                    }
                    break;
                case Key.X:
                    if (_viewModel.SetBalanceMidCommand.CanExecute(null))
                    {
                        _viewModel.SetBalanceMidCommand.Execute(null);
                        return true;
                    }
                    break;
                case Key.C:
                    if (_viewModel.SetBalanceRightCommand.CanExecute(null))
                    {
                        _viewModel.SetBalanceRightCommand.Execute(null);
                        return true;
                    }
                    break;
            }
            return false;
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