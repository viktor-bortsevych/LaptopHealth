using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LaptopHealth.Views
{
    /// <summary>
    /// Interaction logic for KeyboardTestPage.xaml
    /// </summary>
    public partial class KeyboardTestPage : UserControl, ITestPage
    {
        private readonly KeyboardTestPageViewModel _viewModel;
        private readonly Dictionary<Key, Button> _keyButtonMap = [];
        private readonly HashSet<Key> _testedKeys = [];
        private Brush? _primaryBrush;
        private Brush? _whiteBrush;
        private Brush? _successBrush;

        public string TestName => "Keyboard Test";
        public string TestDescription => "Tests keyboard keys and displays pressed keys";

        public KeyboardTestPage(KeyboardTestPageViewModel viewModel)
        {
            LogInfo("[KeyboardTestPage] Constructor called");
            LogInfo($"[KeyboardTestPage] Instance hash: {GetHashCode()}");

            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            LogInfo("[KeyboardTestPage] DataContext set");
            LogInfo($"[KeyboardTestPage] ViewModel type: {_viewModel.GetType().Name}");
            LogInfo($"[KeyboardTestPage] ViewModel hash: {_viewModel.GetHashCode()}");

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            LogInfo("[KeyboardTestPage] Constructor completed");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LogInfo("[KeyboardTestPage] Loaded event fired");

            // Load brushes from resources
            _primaryBrush = TryFindResource("PrimaryBrush") as Brush ?? Brushes.Purple;
            _whiteBrush = TryFindResource("WhiteBrush") as Brush ?? Brushes.White;
            _successBrush = TryFindResource("SuccessBrush") as Brush ?? Brushes.Green;

            LogDebug("[KeyboardTestPage] Brushes loaded");

            // Map all keyboard buttons by their Tag (Key enum value)
            LogDebug("[KeyboardTestPage] Mapping keyboard buttons");
            MapKeyboardButtons(MainStackPanel);
            LogInfo($"[KeyboardTestPage] Mapped {_keyButtonMap.Count} keyboard buttons");

            // Set focus to the control so it can receive keyboard events
            Focus();
            Keyboard.Focus(this);

            // Hook up keyboard events
            AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), true);
            AddHandler(PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp), true);

            LogInfo("[KeyboardTestPage] Keyboard event handlers attached");
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            LogInfo("[KeyboardTestPage] Unloaded event fired");

            // Clean up event handlers
            RemoveHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
            RemoveHandler(PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp));

            LogDebug("[KeyboardTestPage] Keyboard event handlers removed");
            LogInfo("[KeyboardTestPage] Unloaded cleanup completed");
        }

        private void MapKeyboardButtons(DependencyObject parent)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is Button button && button.Tag is string tagString && Enum.TryParse<Key>(tagString, out var key))
                {
                    _keyButtonMap[key] = button;
                }

                // Recursively search children
                MapKeyboardButtons(child);
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

            LogDebug($"[KeyboardTestPage] Key down: {actualKey}");
            _viewModel.HandleKeyDown(e.Key, e.SystemKey);
            SetKeyPressed(actualKey, true);

            e.Handled = true;
        }

        private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

            LogDebug($"[KeyboardTestPage] Key up: {actualKey}");
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
            LogInfo("[KeyboardTestPage] ===============================================================================");
            LogInfo("[KeyboardTestPage] CleanupAsync called");
            LogInfo($"[KeyboardTestPage] Instance hash: {GetHashCode()}");
            LogInfo($"[KeyboardTestPage] ViewModel hash: {_viewModel.GetHashCode()}");
            LogInfo($"[KeyboardTestPage] Tested keys count: {_testedKeys.Count}");

            try
            {
                LogInfo("[KeyboardTestPage] Calling ViewModel.Cleanup");
                _viewModel.Cleanup();
                LogInfo("[KeyboardTestPage] ViewModel.Cleanup completed");

                LogInfo("[KeyboardTestPage] Clearing tested keys");
                _testedKeys.Clear();

                LogInfo("[KeyboardTestPage] CleanupAsync completed successfully");
            }
            catch (Exception ex)
            {
                LogError("[KeyboardTestPage] Error during CleanupAsync", ex);
            }
            finally
            {
                LogInfo("[KeyboardTestPage] ===============================================================================");
            }

            return Task.CompletedTask;
        }

        #region Logging

        private static void LogInfo(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }

        private static void LogDebug(string message)
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
