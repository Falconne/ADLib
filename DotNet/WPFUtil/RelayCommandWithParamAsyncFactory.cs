using ADLib.Util;
using System;
using System.Threading.Tasks;

namespace WPFUtil;

public class RelayCommandWithParamAsyncFactory
{
    public Func<bool>? CanExecute { get; init; }

    // ReSharper disable once MemberCanBePrivate.Global
    public ExceptionHandler ErrorHandler { get; init; } = TopLevelExceptionHandler.ShowError;

    public SafeSynchronizedObject<bool>? SharedBusyIndicator { get; init; }

    public RelayCommandWithParamAsync Create(Func<object?, Task> action)
    {
        var command = new RelayCommandWithParamAsync(
            action,
            ErrorHandler,
            CanExecute);

        if (SharedBusyIndicator != null)
        {
            command.WithSharedBusyIndicator(SharedBusyIndicator);
        }

        return command;
    }
}
