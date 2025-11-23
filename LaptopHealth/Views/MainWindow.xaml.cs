using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LaptopHealth.Services.Infrastructure;
using LaptopHealth.Services.Interfaces;

namespace LaptopHealth.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// Handles navigation between test pages and progress indicators
    /// </summary>
    public partial class MainWindow : Window
    {
        private int currentTestIndex = 0;
        private readonly IReadOnlyList<TestPageInfo> testPages;
        private bool _isNavigating = false;

        public MainWindow()
        {
            InitializeComponent();

            // Load tests from registry
            testPages = TestRegistry.RegisteredTests;

            if (testPages.Count == 0)
            {
                ShowError("No tests registered. Please check your configuration.");
                return;
            }

            InitializeProgressIndicators();
            
            Loaded += async (s, e) => await LoadTestAsync(0);
            
            this.KeyDown += MainWindow_KeyDown;
        }

        private void InitializeProgressIndicators()
        {
            ProgressIndicatorsPanel.Children.Clear();

            for (int i = 0; i < testPages.Count; i++)
            {
                int testIndex = i;

                var button = new Button
                {
                    Tag = testIndex,
                    ToolTip = testPages[i].Name,
                    // Apply the style from resources
                    Style = (Style)Application.Current.FindResource("ProgressIndicatorButtonStyle")
                };

                // Set the active/inactive background color using resources
                if (i == currentTestIndex)
                {
                    button.Background = (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryBrush");
                }
                else
                {
                    button.Background = (System.Windows.Media.Brush)Application.Current.FindResource("InactiveIndicatorBrush");
                }

                button.Click += async (s, e) => await LoadTestAsync(testIndex);

                ProgressIndicatorsPanel.Children.Add(button);
            }
        }

        private async Task LoadTestAsync(int testIndex)
        {
            // Prevent duplicate navigation and concurrent navigation
            if (testIndex < 0 || testIndex >= testPages.Count || _isNavigating)
                return;

            // Prevent navigating to the same page
            if (testIndex == currentTestIndex && TestContentArea.Content != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Already on page {testIndex}, skipping navigation");
                return;
            }

            _isNavigating = true;
            
            try
            {
                currentTestIndex = testIndex;
                var testInfo = testPages[testIndex];
                
                await LoadTestPageAsync(testInfo.PageType);

                UpdateNavigationButtons();
                UpdateProgressIndicators();
            }
            finally
            {
                _isNavigating = false;
            }
        }

        private async Task LoadTestPageAsync(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type pageType)
        {
            try
            {
                // **FIX: Properly cleanup old page before loading new one - AWAIT the cleanup**
                await UnloadCurrentTestPageAsync();
                
                if (System.Activator.CreateInstance(pageType) is UserControl testPage)
                {
                    TestContentArea.Content = testPage;
                }
                else
                {
                    ShowError($"Failed to create instance of {pageType.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading test page: {ex.Message}");
                ShowError($"Failed to load test page: {ex.Message}");
            }
        }

        /// <summary>
        /// Properly unloads the current test page to ensure resources are cleaned up
        /// </summary>
        private async Task UnloadCurrentTestPageAsync()
        {
            if (TestContentArea.Content is UserControl oldPage)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Unloading page: {oldPage.GetType().Name}");
                
                // If page implements ITestPage, call its async cleanup method
                if (oldPage is ITestPage testPage)
                {
                    try
                    {
                        await testPage.CleanupAsync();
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] CleanupAsync completed for {oldPage.GetType().Name}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Error during CleanupAsync: {ex.Message}");
                    }
                }
                
                // Clear content - this will trigger Unloaded event
                TestContentArea.Content = null;
                
                // Force layout update to ensure Unloaded event fires
                TestContentArea.UpdateLayout();
                
                // If the page implements IDisposable, dispose it
                if (oldPage is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Page unloaded: {oldPage.GetType().Name}");
            }
        }

        private void ShowError(string message)
        {
            TestContentArea.Content = new TextBlock
            {
                Text = message,
                Foreground = System.Windows.Media.Brushes.Red,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20),
                FontSize = 14
            };
        }

        private void UpdateProgressIndicators()
        {
            for (int i = 0; i < ProgressIndicatorsPanel.Children.Count; i++)
            {
                if (ProgressIndicatorsPanel.Children[i] is Button button)
                {
                    if (i == currentTestIndex)
                    {
                        button.Background = (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryBrush");
                    }
                    else
                    {
                        button.Background = (System.Windows.Media.Brush)Application.Current.FindResource("InactiveIndicatorBrush");
                    }
                }
            }
        }

        private void UpdateNavigationButtons()
        {
            PreviousButton.IsEnabled = currentTestIndex > 0;
            NextButton.IsEnabled = currentTestIndex < testPages.Count - 1;
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTestIndex > 0)
            {
                await LoadTestAsync(currentTestIndex - 1);
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTestIndex < testPages.Count - 1)
            {
                await LoadTestAsync(currentTestIndex + 1);
            }
        }

        private async void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                case Key.Up:
                    if (currentTestIndex > 0)
                    {
                        await LoadTestAsync(currentTestIndex - 1);
                    }
                    e.Handled = true;
                    break;

                case Key.Right:
                case Key.Down:
                    if (currentTestIndex < testPages.Count - 1)
                    {
                        await LoadTestAsync(currentTestIndex + 1);
                    }
                    e.Handled = true;
                    break;
            }
        }
    }
}
