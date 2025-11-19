using System.Windows;
using System.Windows.Controls;
using LaptopHealth.Services.Interfaces;

namespace LaptopHealth.Views
{
    /// <summary>
    /// Interaction logic for CounterTestPage.xaml
    /// </summary>
    public partial class CounterTestPage : UserControl, ITestPage
    {
        private readonly ICounterService _counterService;

        public string TestName => "Counter Test";
        public string TestDescription => "Tests counting functionality";

        public CounterTestPage()
        {
            InitializeComponent();
            
            _counterService = App.ServiceProvider?.GetService(typeof(ICounterService)) as ICounterService
                ?? throw new InvalidOperationException("ICounterService is not registered");
            
            UpdateDisplay();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            _counterService.Increment();
            UpdateDisplay();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _counterService.Reset();
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            CounterDisplay.Text = _counterService.GetCurrentCount().ToString();
        }
    }
}
