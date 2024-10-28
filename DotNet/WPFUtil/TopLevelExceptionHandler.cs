using ADLib.Logging;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WPFUtil;

public delegate void ExceptionHandler(Exception exception);

public static class TopLevelExceptionHandler
{
    public static ExceptionHandler Attach()
    {
        AppDomain.CurrentDomain.UnhandledException += HandleGlobalException;
        Application.Current.DispatcherUnhandledException += HandleMainThreadException;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

        return ShowError;
    }

    private static void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        var innerException = e.Exception.InnerException;
        ShowError(innerException ?? e.Exception);
    }

    private static void HandleMainThreadException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowError(e.Exception);
    }

    private static void HandleGlobalException(object sender, UnhandledExceptionEventArgs args)
    {
        ShowError((Exception)args.ExceptionObject);
    }

    private static void ShowError(Exception e)
    {
        GenLog.Error($"Unhandled Exception ({e.GetType()}): {e.Message}");
        if (e.StackTrace != null)
        {
            GenLog.Error(e.StackTrace);
        }

        MessageBox.Show(
            $"{e.Message}",
            $"Unhandled Exception ({e.GetType()})",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Environment.Exit(1);
    }
}