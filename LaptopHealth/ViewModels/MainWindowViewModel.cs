using LaptopHealth.Services.Infrastructure;
using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels.Infrastructure;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;

namespace LaptopHealth.ViewModels
{
    /// <summary>
    /// ViewModel for MainWindow - handles navigation and test page management
    /// </summary>
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IReadOnlyList<TestPageInfo> _testPages;
        private readonly IServiceProvider _serviceProvider;
        private int _currentTestIndex;
        private UserControl? _currentTestPage;
        private bool _isNavigating;
        private string _errorMessage = string.Empty;
        private bool _hasError;

        /// <summary>
        /// Gets the collection of progress indicators
        /// </summary>
        public ObservableCollection<ProgressIndicatorItem> ProgressIndicators { get; } = [];

        /// <summary>
        /// Gets or sets the current test page content
        /// </summary>
        public UserControl? CurrentTestPage
        {
            get => _currentTestPage;
            private set => SetProperty(ref _currentTestPage, value);
        }

        /// <summary>
        /// Gets or sets the current test index
        /// </summary>
        public int CurrentTestIndex
        {
            get => _currentTestIndex;
            private set
            {
                if (SetProperty(ref _currentTestIndex, value))
                {
                    UpdateNavigationButtonStates();
                    UpdateProgressIndicators();
                }
            }
        }

        /// <summary>
        /// Gets whether the Previous button should be enabled
        /// </summary>
        public bool CanNavigatePrevious => _currentTestIndex > 0 && !_isNavigating;

        /// <summary>
        /// Gets whether the Next button should be enabled
        /// </summary>
        public bool CanNavigateNext => _currentTestIndex < _testPages.Count - 1 && !_isNavigating;

        /// <summary>
        /// Gets or sets the error message
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Gets or sets whether there is an error
        /// </summary>
        public bool HasError
        {
            get => _hasError;
            private set => SetProperty(ref _hasError, value);
        }

        /// <summary>
        /// Command to navigate to the previous test
        /// </summary>
        public ICommand PreviousCommand { get; }

        /// <summary>
        /// Command to navigate to the next test
        /// </summary>
        public ICommand NextCommand { get; }

        /// <summary>
        /// Command to navigate to a specific test by index
        /// </summary>
        public ICommand NavigateToTestCommand { get; }

        /// <summary>
        /// Command to handle key navigation
        /// </summary>
        public ICommand KeyNavigationCommand { get; }

        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Load tests from registry
            _testPages = TestRegistry.RegisteredTests;

            LogInfo("===============================================================================");
            LogInfo("MainWindowViewModel: Initialization started");
            LogInfo($"MainWindowViewModel: Total registered tests: {_testPages.Count}");

            if (_testPages.Count == 0)
            {
                LogError("MainWindowViewModel: No tests registered. Please check your configuration.");
                ShowError("No tests registered. Please check your configuration.");
                PreviousCommand = new AsyncRelayCommand(_ => Task.CompletedTask, _ => false);
                NextCommand = new AsyncRelayCommand(_ => Task.CompletedTask, _ => false);
                NavigateToTestCommand = new AsyncRelayCommand(_ => Task.CompletedTask, _ => false);
                KeyNavigationCommand = new AsyncRelayCommand(_ => Task.CompletedTask, _ => false);
                return;
            }

            // Log registered tests
            for (int i = 0; i < _testPages.Count; i++)
            {
                LogDebug($"MainWindowViewModel: Test {i}: {_testPages[i].Name} (Type: {_testPages[i].PageType.Name})");
            }

            // Initialize commands
            NavigateToTestCommand = new AsyncRelayCommand(
                async param => await NavigateToTestAsync(Convert.ToInt32(param)),
                param => !_isNavigating
            );

            PreviousCommand = new AsyncRelayCommand(
                async _ => await NavigateToPreviousAsync(),
                _ => CanNavigatePrevious
            );

            NextCommand = new AsyncRelayCommand(
                async _ => await NavigateToNextAsync(),
                _ => CanNavigateNext
            );

            KeyNavigationCommand = new AsyncRelayCommand(
                async param => await HandleKeyNavigationAsync(param as KeyEventArgs),
                _ => !_isNavigating
            );

            // Initialize progress indicators
            InitializeProgressIndicators();
            LogInfo("MainWindowViewModel: Initialization completed");
        }

        /// <summary>
        /// Initializes the view model and loads the first test
        /// </summary>
        public async Task InitializeAsync()
        {
            LogInfo("MainWindowViewModel.InitializeAsync: Starting");
            if (_testPages.Count > 0 && CurrentTestPage == null)
            {
                LogInfo("MainWindowViewModel.InitializeAsync: Loading first test (index 0)");
                await LoadTestAsync(0);
            }
            LogInfo("MainWindowViewModel.InitializeAsync: Completed");
        }

        /// <summary>
        /// Initializes the progress indicator buttons
        /// </summary>
        private void InitializeProgressIndicators()
        {
            LogDebug("MainWindowViewModel: Initializing progress indicators");
            var primaryBrush = (Brush)Application.Current.FindResource("PrimaryBrush");
            var inactiveBrush = (Brush)Application.Current.FindResource("InactiveIndicatorBrush");

            for (int i = 0; i < _testPages.Count; i++)
            {
                var indicator = new ProgressIndicatorItem(i, _testPages[i].Name, NavigateToTestCommand)
                {
                    IsActive = i == _currentTestIndex,
                    Background = i == _currentTestIndex ? primaryBrush : inactiveBrush
                };

                ProgressIndicators.Add(indicator);
                LogDebug($"MainWindowViewModel: Created progress indicator {i} for {_testPages[i].Name}");
            }
        }

        /// <summary>
        /// Navigates to a specific test by index
        /// </summary>
        private async Task NavigateToTestAsync(int testIndex)
        {
            LogInfo($"MainWindowViewModel: NavigateToTestAsync called for index {testIndex}");
            await LoadTestAsync(testIndex);
        }

        /// <summary>
        /// Navigates to the previous test
        /// </summary>
        private async Task NavigateToPreviousAsync()
        {
            LogInfo($"MainWindowViewModel: NavigateToPreviousAsync - current index: {CurrentTestIndex}");
            if (CanNavigatePrevious)
            {
                await LoadTestAsync(CurrentTestIndex - 1);
            }
        }

        /// <summary>
        /// Navigates to the next test
        /// </summary>
        private async Task NavigateToNextAsync()
        {
            LogInfo($"MainWindowViewModel: NavigateToNextAsync - current index: {CurrentTestIndex}");
            if (CanNavigateNext)
            {
                await LoadTestAsync(CurrentTestIndex + 1);
            }
        }

        /// <summary>
        /// Handles keyboard navigation
        /// </summary>
        private async Task HandleKeyNavigationAsync(KeyEventArgs? e)
        {
            if (e == null)
                return;

            switch (e.Key)
            {
                case Key.Left:
                case Key.Up:
                    LogDebug($"MainWindowViewModel: Keyboard navigation - {e.Key} key pressed");
                    if (CanNavigatePrevious)
                    {
                        await NavigateToPreviousAsync();
                    }
                    e.Handled = true;
                    break;

                case Key.Right:
                case Key.Down:
                    LogDebug($"MainWindowViewModel: Keyboard navigation - {e.Key} key pressed");
                    if (CanNavigateNext)
                    {
                        await NavigateToNextAsync();
                    }
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Loads a test page by index
        /// </summary>
        private async Task LoadTestAsync(int testIndex)
        {
            // Prevent duplicate navigation and concurrent navigation
            if (testIndex < 0 || testIndex >= _testPages.Count || _isNavigating)
            {
                LogDebug($"MainWindowViewModel: LoadTestAsync skipped - invalid index or already navigating");
                return;
            }

            // Prevent navigating to the same page
            if (testIndex == CurrentTestIndex && CurrentTestPage != null)
            {
                LogDebug($"MainWindowViewModel: Already on page {testIndex}, skipping navigation");
                return;
            }

            LogInfo($"===============================================================================");
            LogInfo($"MainWindowViewModel: LoadTestAsync starting - target index: {testIndex}");
            LogInfo($"MainWindowViewModel: Current index: {CurrentTestIndex}");
            LogInfo($"MainWindowViewModel: Current page type: {CurrentTestPage?.GetType().Name ?? "null"}");

            _isNavigating = true;

            try
            {
                var testInfo = _testPages[testIndex];
                LogInfo($"MainWindowViewModel: Test to load: {testInfo.Name} (Type: {testInfo.PageType.Name})");

                await LoadTestPageAsync(testInfo.PageType);

                CurrentTestIndex = testIndex;
                HasError = false;

                LogInfo($"MainWindowViewModel: Successfully loaded page {testIndex}");
                LogInfo($"MainWindowViewModel: Current page type: {CurrentTestPage?.GetType().Name ?? "null"}");
            }
            catch (Exception ex)
            {
                LogError($"MainWindowViewModel: LoadTestAsync failed - {ex.Message}");
                ShowError($"Failed to load test: {ex.Message}");
            }
            finally
            {
                _isNavigating = false;
                UpdateNavigationButtonStates();
                LogInfo($"MainWindowViewModel: LoadTestAsync completed");
                LogInfo($"===============================================================================");
            }
        }

        /// <summary>
        /// Loads a test page by type
        /// </summary>
        private async Task LoadTestPageAsync(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type pageType)
        {
            LogDebug($"MainWindowViewModel: LoadTestPageAsync - pageType: {pageType.Name}");
            
            try
            {
                // Cleanup old page before loading new one
                LogDebug($"MainWindowViewModel: Unloading old page before loading new one");
                await UnloadCurrentTestPageAsync();

                LogDebug($"MainWindowViewModel: Creating instance of {pageType.Name}");
                if (_serviceProvider.GetRequiredService(pageType) is UserControl testPage)
                {
                    LogInfo($"MainWindowViewModel: Successfully created instance of {pageType.Name}");
                    LogDebug($"MainWindowViewModel: Instance type: {testPage.GetType().FullName}");
                    CurrentTestPage = testPage;
                }
                else
                {
                    LogError($"MainWindowViewModel: Failed to create instance of {pageType.Name} - not a UserControl");
                    ShowError($"Failed to create instance of {pageType.Name}");
                }
            }
            catch (Exception ex)
            {
                LogError($"MainWindowViewModel: Error loading test page - {ex.Message}");
                ShowError($"Failed to load test page: {ex.Message}");
            }
        }

        /// <summary>
        /// Properly unloads the current test page to ensure resources are cleaned up
        /// </summary>
        private async Task UnloadCurrentTestPageAsync()
        {
            if (CurrentTestPage == null)
            {
                LogDebug($"MainWindowViewModel: UnloadCurrentTestPageAsync - no page to unload");
                return;
            }

            var oldPage = CurrentTestPage;
            LogInfo($"MainWindowViewModel: UnloadCurrentTestPageAsync starting - page type: {oldPage.GetType().Name}");
            LogDebug($"MainWindowViewModel: Page instance hash code: {oldPage.GetHashCode()}");

            // If page implements ITestPage, call its async cleanup method
            if (oldPage is ITestPage testPage)
            {
                try
                {
                    LogDebug($"MainWindowViewModel: Calling CleanupAsync on {oldPage.GetType().Name}");
                    await testPage.CleanupAsync();
                    LogInfo($"MainWindowViewModel: CleanupAsync completed for {oldPage.GetType().Name}");
                }
                catch (Exception ex)
                {
                    LogError($"MainWindowViewModel: Error during CleanupAsync on {oldPage.GetType().Name} - {ex.Message}");
                }
            }
            else
            {
                LogDebug($"MainWindowViewModel: Page {oldPage.GetType().Name} does not implement ITestPage");
            }

            // Clear current page reference
            LogDebug($"MainWindowViewModel: Clearing CurrentTestPage reference");
            CurrentTestPage = null;

            // If the page implements IDisposable, dispose it
            if (oldPage is IDisposable disposable)
            {
                try
                {
                    LogDebug($"MainWindowViewModel: Disposing {oldPage.GetType().Name}");
                    disposable.Dispose();
                    LogInfo($"MainWindowViewModel: Disposed {oldPage.GetType().Name}");
                }
                catch (Exception ex)
                {
                    LogError($"MainWindowViewModel: Error disposing {oldPage.GetType().Name} - {ex.Message}");
                }
            }

            LogInfo($"MainWindowViewModel: UnloadCurrentTestPageAsync completed - page type: {oldPage.GetType().Name}");
        }

        /// <summary>
        /// Shows an error message
        /// </summary>
        private void ShowError(string message)
        {
            ErrorMessage = message;
            HasError = true;
        }

        /// <summary>
        /// Updates the navigation button states
        /// </summary>
        private void UpdateNavigationButtonStates()
        {
            OnPropertyChanged(nameof(CanNavigatePrevious));
            OnPropertyChanged(nameof(CanNavigateNext));
        }

        /// <summary>
        /// Updates the progress indicators to reflect the current test
        /// </summary>
        private void UpdateProgressIndicators()
        {
            var primaryBrush = (Brush)Application.Current.FindResource("PrimaryBrush");
            var inactiveBrush = (Brush)Application.Current.FindResource("InactiveIndicatorBrush");

            for (int i = 0; i < ProgressIndicators.Count; i++)
            {
                var indicator = ProgressIndicators[i];
                indicator.IsActive = i == CurrentTestIndex;
                indicator.Background = i == CurrentTestIndex ? primaryBrush : inactiveBrush;
            }
        }

        #region Logging Helper Methods

        private static void LogInfo(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
        }

        private static void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] {message}");
        }

        private static void LogError(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] {message}");
        }

        #endregion
    }
}
