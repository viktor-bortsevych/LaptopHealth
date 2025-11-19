using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LaptopHealth.Services;

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
            LoadTest(0);
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
                    Width = 12,
                    Height = 12,
                    Padding = new Thickness(0),
                    Margin = new Thickness(6, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(0),
                    Background = i == currentTestIndex ?
                        System.Windows.Media.Brushes.DodgerBlue :
                        System.Windows.Media.Brushes.LightGray,
                    Tag = testIndex,
                    ToolTip = testPages[i].Name // Add tooltip for better UX
                };

                var style = new Style { TargetType = typeof(Button) };
                style.Setters.Add(new Setter(Button.TemplateProperty, CircleButtonTemplate));
                button.Style = style;

                button.Click += (s, e) => LoadTest(testIndex);

                ProgressIndicatorsPanel.Children.Add(button);
            }
        }

        private static ControlTemplate CircleButtonTemplate
        {
            get
            {
                var template = new ControlTemplate(typeof(Button));
                var border = new FrameworkElementFactory(typeof(Border));

                border.SetValue(Border.BackgroundProperty,
                    new System.Windows.Data.Binding("Background")
                    {
                        RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent
                    });
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
                border.SetValue(WidthProperty, 12.0);
                border.SetValue(HeightProperty, 12.0);

                template.VisualTree = border;
                return template;
            }
        }

        private void LoadTest(int testIndex)
        {
            if (testIndex < 0 || testIndex >= testPages.Count)
                return;

            currentTestIndex = testIndex;

            var testInfo = testPages[testIndex];
            LoadTestPage(testInfo.PageType);

            UpdateNavigationButtons();
            UpdateProgressIndicators();
        }

        private void LoadTestPage(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type pageType)
        {
            try
            {
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
                    button.Background = i == currentTestIndex ?
                        System.Windows.Media.Brushes.DodgerBlue :
                        System.Windows.Media.Brushes.LightGray;
                }
            }
        }

        private void UpdateNavigationButtons()
        {
            PreviousButton.IsEnabled = currentTestIndex > 0;
            NextButton.IsEnabled = currentTestIndex < testPages.Count - 1;
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTestIndex > 0)
            {
                LoadTest(currentTestIndex - 1);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentTestIndex < testPages.Count - 1)
            {
                LoadTest(currentTestIndex + 1);
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                case Key.Up:
                    if (currentTestIndex > 0)
                    {
                        LoadTest(currentTestIndex - 1);
                    }
                    e.Handled = true;
                    break;

                case Key.Right:
                case Key.Down:
                    if (currentTestIndex < testPages.Count - 1)
                    {
                        LoadTest(currentTestIndex + 1);
                    }
                    e.Handled = true;
                    break;
            }
        }
    }
}
