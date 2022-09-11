using ADLib.Util;
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

        private SafeSynchronizedObject<bool> _busy = new(false);

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

        public RelayCommandAsync WithSharedBusyIndicator(SafeSynchronizedObject<bool> busy)
        {
            _busy = busy;
            return this;
        }

        public bool CanExecute(object? parameter)
        {
            return !_busy.Get() && (_canExecute?.Invoke() ?? true);
        }

        public void Execute(object? parameter)
        {
            _busy.Set(true);
            _execute().FireAndForget(_errorHandler, () => _busy.Set(false));
        }
    }
}