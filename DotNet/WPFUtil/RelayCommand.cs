using System;
using System.Windows.Input;

namespace WPFUtil;

public class RelayCommandFactory
{
    public RelayCommandFactory(Func<bool> canExecute)
    {
        _canExecute = canExecute;
    }

    private readonly Func<bool> _canExecute;

    public RelayCommand Create(Action action)
    {
        return new RelayCommand(action, _canExecute);
    }
}

public class RelayCommand : ICommand
{
    public RelayCommand(Action action, Func<bool>? canExecute = null)
    {
        _executeWithNoParam = action;
        _canExecute = canExecute;
    }

    public RelayCommand(Action<object?> action, Func<bool>? canExecute = null)
    {
        _executeWithParam = action;
        _canExecute = canExecute;
    }

    private readonly Action? _executeWithNoParam = null;

    private readonly Action<object?>? _executeWithParam = null;

    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;

        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        if (_executeWithNoParam != null)
        {
            _executeWithNoParam();
        }
        else
        {
            _executeWithParam?.Invoke(parameter);
        }
    }
}