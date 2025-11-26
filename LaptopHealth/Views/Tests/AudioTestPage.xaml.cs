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
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
        }

        public async Task CleanupAsync()
        {
            await _viewModel.DisposeAsync();
        }
    }
}