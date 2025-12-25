using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace WPFUtil;

public static class WPFHelpers
{
    public static async Task InvokeInUIThreadAsync(Func<Task> action)
    {
        await Application.Current.Dispatcher.InvokeAsync(async () => await action().ConfigureAwait(false));
    }

    public static void InvokeInUIThread(Action action)
    {
        Application.Current.Dispatcher.Invoke(action);
    }

    public static void RefreshCommandsState()
    {
        InvokeInUIThread(CommandManager.InvalidateRequerySuggested);
    }
}