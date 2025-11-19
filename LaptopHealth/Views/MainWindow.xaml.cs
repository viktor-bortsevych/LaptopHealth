using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace LaptopHealth.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int currentTestIndex = 0;
        private List<string> tests = new List<string> 
        { 
            "Test 1", 
            "Test 2", 
            "Test 3", 
            "Test 4" 
        };

        public MainWindow()
        {
            InitializeComponent();
            InitializeProgressIndicators();
            LoadTest(0);
            this.KeyDown += MainWindow_KeyDown;
        }

        private void InitializeProgressIndicators()
        {
            ProgressIndicatorsPanel.Children.Clear();
            for (int i = 0; i < tests.Count; i++)
            {
                int testIndex = i; // Capture for closure
                
                var button = new Button
                {
                    Width = 12,
                    Height = 12,
                    Padding = new Thickness(0),
                    Margin = new Thickness(6, 0, 6, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    BorderThickness = new Thickness(0),
                    Background = i == currentTestIndex ? 
                        System.Windows.Media.Brushes.DodgerBlue : 
                        System.Windows.Media.Brushes.LightGray,
                    Tag = testIndex
                };

                var style = new System.Windows.Style { TargetType = typeof(Button) };
                style.Setters.Add(new System.Windows.Setter(Button.TemplateProperty, GetCircleButtonTemplate()));
                button.Style = style;

                button.Click += (s, e) =>
                {
                    LoadTest(testIndex);
                };

                ProgressIndicatorsPanel.Children.Add(button);
            }
        }

        private System.Windows.Controls.ControlTemplate GetCircleButtonTemplate()
        {
            var template = new System.Windows.Controls.ControlTemplate(typeof(Button));
            var border = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            
            border.SetValue(System.Windows.Controls.Border.BackgroundProperty, 
                new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            border.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(System.Windows.Controls.Border.WidthProperty, 12.0);
            border.SetValue(System.Windows.Controls.Border.HeightProperty, 12.0);
            
            template.VisualTree = border;
            return template;
        }

        private void LoadTest(int testIndex)
        {
            if (testIndex < 0 || testIndex >= tests.Count)
                return;

            currentTestIndex = testIndex;
            CurrentTestName.Text = tests[testIndex];
            CurrentTestDescription.Text = $"This is the description for {tests[testIndex]}";
            
            UpdateNavigationButtons();
            InitializeProgressIndicators();
        }

        private void UpdateNavigationButtons()
        {
            PreviousButton.IsEnabled = currentTestIndex > 0;
            NextButton.IsEnabled = currentTestIndex < tests.Count - 1;
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
            if (currentTestIndex < tests.Count - 1)
            {
                LoadTest(currentTestIndex + 1);
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Up)
            {
                if (currentTestIndex > 0)
                {
                    LoadTest(currentTestIndex - 1);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Right || e.Key == Key.Down)
            {
                if (currentTestIndex < tests.Count - 1)
                {
                    LoadTest(currentTestIndex + 1);
                }
                e.Handled = true;
            }
        }
    }
}
