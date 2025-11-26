using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels;

namespace LaptopHealth.Views
{
    /// <summary>
    /// Interaction logic for KeyboardTestPage.xaml
    /// </summary>
    public partial class KeyboardTestPage : UserControl, ITestPage
    {
        private readonly KeyboardTestPageViewModel _viewModel;
        private readonly Dictionary<Key, Button> _keyButtonMap = new();
        private readonly HashSet<Key> _testedKeys = new();
        private Brush? _primaryBrush;
        private Brush? _whiteBrush;
        private Brush? _successBrush;
        private Brush? _darkGrayBrush;

        public string TestName => "Keyboard Test";
        public string TestDescription => "Tests keyboard keys and displays pressed keys";

        public KeyboardTestPage(KeyboardTestPageViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Load brushes from resources
            _primaryBrush = TryFindResource("PrimaryBrush") as Brush ?? Brushes.Purple;
            _whiteBrush = TryFindResource("WhiteBrush") as Brush ?? Brushes.White;
            _successBrush = TryFindResource("SuccessBrush") as Brush ?? Brushes.Green;
            _darkGrayBrush = TryFindResource("DarkGrayBrush") as Brush ?? Brushes.Gray;

            // Map all keyboard buttons by their Tag (Key enum value)
            MapKeyboardButtons(MainStackPanel);

            // Set focus to the control so it can receive keyboard events
            Focus();
            Keyboard.Focus(this);

            // Hook up keyboard events
            AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), true);
            AddHandler(PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp), true);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Clean up event handlers
            RemoveHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
            RemoveHandler(PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp));
        }

        private void MapKeyboardButtons(DependencyObject parent)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is Button button && button.Tag is string tagString)
                {
                    if (Enum.TryParse<Key>(tagString, out var key))
                    {
                        _keyButtonMap[key] = button;
                    }
                }

                // Recursively search children
                MapKeyboardButtons(child);
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            
            _viewModel.HandleKeyDown(e.Key, e.SystemKey);
            SetKeyPressed(actualKey, true);
            
            e.Handled = true;
        }

        private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            
            _viewModel.HandleKeyUp(e.Key, e.SystemKey);
            SetKeyPressed(actualKey, false);
            
            e.Handled = true;
        }

        private void SetKeyPressed(Key key, bool isPressed)
        {
            if (_keyButtonMap.TryGetValue(key, out var button))
            {
                if (isPressed)
                {
                    // Mark as tested
                    _testedKeys.Add(key);
                    
                    // State: Currently Pressed (Outline style with primary color)
                    button.Background = Brushes.Transparent;
                    button.BorderBrush = _primaryBrush;
                    button.BorderThickness = new Thickness(2);
                    button.Foreground = _primaryBrush;
                }
                else
                {
                    // State: Tested (Green filled)
                    button.Background = _successBrush;
                    button.BorderBrush = _successBrush;
                    button.BorderThickness = new Thickness(1);
                    button.Foreground = _whiteBrush;
                }
            }
        }

        /// <summary>
        /// Performs async cleanup before page unload. Called by MainWindow before navigation.
        /// </summary>
        public Task CleanupAsync()
        {
            _viewModel.Cleanup();
            return Task.CompletedTask;
        }
    }
}
