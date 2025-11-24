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
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            // Initialize when loaded
            Loaded += async (s, e) => await _viewModel.InitializeAsync();
        }

        /// <summary>
        /// Performs async cleanup before page unload. Called by MainWindow before navigation.
        /// </summary>
        public async Task CleanupAsync()
        {
            await _viewModel.CleanupAsync();
            _viewModel.Dispose();
        }
    }
}
