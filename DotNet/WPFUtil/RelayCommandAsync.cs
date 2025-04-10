using ADLib.Util;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace WPFUtil;

public class RelayCommandAsync : ICommand
{
    public RelayCommandAsync(
        Func<Task> action,
        ExceptionHandler? errorHandler = null,
        Func<bool>? canExecute = null)
    {
        _execute = action;
        _errorHandler = errorHandler ?? TopLevelExceptionHandler.ShowError;
        _canExecute = canExecute;
    }

    private readonly Func<bool>? _canExecute;

    private readonly ExceptionHandler _errorHandler;

    private readonly Func<Task> _execute;

    private SafeSynchronizedObject<bool> _busy = new(false);

    private Action? _postFunction;

    private Action? _preFunction;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;

        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return !_busy.Get() && (_canExecute?.Invoke() ?? true);
    }

    public void Execute(object? parameter)
    {
        _busy.Set(true);
        _preFunction?.Invoke();
        _execute().FireAndForget(_errorHandler, () => _busy.Set(false));
    }

    public RelayCommandAsync WithSharedBusyIndicator(SafeSynchronizedObject<bool> busy)
    {
        _busy = busy;
        return this;
    }

    public RelayCommandAsync WithPreFunction(Action? function)
    {
        _preFunction = function;
        return this;
    }

    public RelayCommandAsync WithPostFunction(Action? function)
    {
        _postFunction = function;
        return this;
    }

    public void DoPostActions()
    {
        _busy.Set(false);
        Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
        _postFunction?.Invoke();
    }
}