using System.Windows.Input;

namespace LaptopHealth.ViewModels.Infrastructure
{
    /// <summary>
    /// A command implementation that delegates execution and can evaluate execution logic
    /// </summary>
    public class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
    {
        private readonly Action<object?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Func<object?, bool>? _canExecute = canExecute;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// Determines whether the command can execute in its current state
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// <summary>
        /// Executes the command logic
        /// </summary>
        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// Raises the CanExecuteChanged event
        /// </summary>
        public static void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// A generic command implementation for async operations
    /// </summary>
    public class AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null) : ICommand
    {
        private readonly Func<object?, Task> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Func<object?, bool>? _canExecute = canExecute;
        private bool _isExecuting;
        private CancellationTokenSource? _cts;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
        /// Determines whether the command can execute in its current state
        /// </summary>
        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute(parameter));
        }

        /// <summary>
        /// Executes the command logic asynchronously with cancellation support
        /// </summary>
        public async void Execute(object? parameter)
        {
            if (_isExecuting)
                return;

            // Cancel previous execution if still running
            _cts?.CancelAsync();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute(parameter);
            }
            catch (OperationCanceledException)
            {
                // Expected when command is cancelled
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Cancels the currently executing command
        /// </summary>
        public void Cancel()
        {
            _cts?.Cancel();
        }

        /// <summary>
        /// Raises the CanExecuteChanged event
        /// </summary>
        public static void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
