using System;
using System.Threading.Tasks;
using System.Windows;

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
}