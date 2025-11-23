using System.Windows;
using System.Windows.Controls;
using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels;

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