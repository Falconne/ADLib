
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WPFUtil
{
    public class RelayCommandAsync : ICommand
    {
        private readonly Func<Task> _execute;

        private readonly Func<bool>? _canExecute;

        private readonly Action<Exception> _errorHandler;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommandAsync(Func<Task> action, Action<Exception> errorHandler, Func<bool>? canExecute = null)
        {
            _execute = action;
            _errorHandler = errorHandler;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute().FireAndForget(_errorHandler);
        }
    }
}