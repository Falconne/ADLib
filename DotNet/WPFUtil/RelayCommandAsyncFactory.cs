using ADLib.Util;
using System;
using System.Threading.Tasks;

namespace WPFUtil;

public class RelayCommandAsyncFactory
{
    public Func<bool>? CanExecute { get; init; }

    // ReSharper disable once MemberCanBePrivate.Global
    public ExceptionHandler ErrorHandler { get; init; } = TopLevelExceptionHandler.ShowError;

    public SafeSynchronizedObject<bool>? SharedBusyIndicator { get; init; }

    public RelayCommandAsync Create(Func<Task> action)
    {
        var command = new RelayCommandAsync(
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