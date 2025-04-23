using ADLib.Util;
using System;
using System.Threading.Tasks;

namespace WPFUtil;

public class RelayCommandFactory
{
    public Func<bool>? CanExecute { get; set; }

    public ExceptionHandler ErrorHandler { get; set; } = TopLevelExceptionHandler.ShowError;

    public SafeSynchronizedObject<bool>? SharedBusyIndicator { get; set; }

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