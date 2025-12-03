using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels;
using System.Windows;
using System.Windows.Input;

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
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // First, check if the current page is a keyboard shortcut handler
            if (_viewModel.CurrentTestPage is IKeyboardShortcutHandler shortcutHandler)
            {
                if (shortcutHandler.HandleKeyDown(e.Key))
                {
                    e.Handled = true;
                    return;
                }
            }

            // Then handle navigation commands
            if (_viewModel.KeyNavigationCommand.CanExecute(e))
            {
                _viewModel.KeyNavigationCommand.Execute(e);
            }
        }
    }
}
