using LaptopHealth.ViewModels.Infrastructure;
using System.Windows.Input;
using System.Windows.Media;

namespace LaptopHealth.ViewModels
{
    /// <summary>
    /// Represents a progress indicator button in the navigation bar
    /// </summary>
    public class ProgressIndicatorItem(int testIndex, string testName, ICommand navigateCommand) : ViewModelBase
    {
        private bool _isActive;
        private Brush? _background;

        /// <summary>
        /// Gets the test index
        /// </summary>
        public int TestIndex { get; } = testIndex;

        /// <summary>
        /// Gets the test name for tooltip
        /// </summary>
        public string TestName { get; } = testName;

        /// <summary>
        /// Gets or sets whether this indicator is active
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        /// Gets or sets the background brush
        /// </summary>
        public Brush? Background
        {
            get => _background;
            set => SetProperty(ref _background, value);
        }

        /// <summary>
        /// Command to navigate to this test
        /// </summary>
        public ICommand NavigateCommand { get; } = navigateCommand;
    }
}
