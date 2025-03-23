using System;
using System.Windows.Input;

namespace WPFUtil;

public class RelayCommand : ICommand
{
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _executeWithNoParam = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action<object?> execute, Func<bool>? canExecute = null)
    {
        _executeWithParam = execute;
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
        else if (_executeWithParam != null)
        {
            _executeWithParam(parameter);
        }
    }
}