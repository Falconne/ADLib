using ADLib.Util;
using System;
using System.Threading.Tasks;

namespace WPFUtil;

public abstract class InteractiveViewModelBase : PropertyContainerBase
{
    protected InteractiveViewModelBase(
        Action<Exception> exceptionHandler,
        SafeSynchronizedObject<bool>? shareBusyIndicator = null)
    {
        if (shareBusyIndicator != null)
            BusyIndicator = shareBusyIndicator;

        ExceptionHandler = exceptionHandler;
    }

    protected Action<Exception> ExceptionHandler { get; }

    protected readonly SafeSynchronizedObject<bool> BusyIndicator = new(false);

    protected bool CanExecuteAnyAction()
    {
        return !BusyIndicator.Get();
    }

    protected RelayCommand CreateStandardCommand(Action action)
    {
        return new RelayCommand(action, CanExecuteAnyAction);
    }

    protected RelayCommandAsync CreateStandardAsyncCommand(Func<Task> action)
    {
        return new RelayCommandAsync(
                action,
                ExceptionHandler,
                CanExecuteAnyAction)
            .WithSharedBusyIndicator(BusyIndicator);
    }
}