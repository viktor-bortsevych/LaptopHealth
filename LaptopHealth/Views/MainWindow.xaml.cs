using System.Windows;
using System.Windows.Input;
using LaptopHealth.ViewModels;

namespace LaptopHealth.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;

        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;
            
            // Initialize the ViewModel after the window is loaded
            Loaded += async (s, e) => await _viewModel.InitializeAsync();

            // Handle key events and delegate to ViewModel
            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_viewModel.KeyNavigationCommand.CanExecute(e))
            {
                _viewModel.KeyNavigationCommand.Execute(e);
            }
        }
    }
}
