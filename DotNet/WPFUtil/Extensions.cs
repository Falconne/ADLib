using System;
using System.Threading.Tasks;

namespace WPFUtil;

public static class Extensions
{
#pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void
    public static async void FireAndForget(
        this Task task,
        ExceptionHandler? errorHandler = null,
        Action? onActionCompleted = null)
#pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void
    {
        try
        {
            await task;
            onActionCompleted?.Invoke();
        }
        catch (Exception e)
        {
            if (errorHandler != null)
            {
                errorHandler(e);
            }
            else
            {
                TopLevelExceptionHandler.ShowError(e);
            }
        }
    }

#pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void
    public static async void FireAndForgetOnOtherThread(
        this Task task,
        ExceptionHandler? errorHandler = null,
        Action? onActionCompleted = null)
#pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void
    {
        try
        {
            await task.ConfigureAwait(false);
            onActionCompleted?.Invoke();
        }
        catch (Exception e)
        {
            if (errorHandler != null)
            {
                errorHandler(e);
            }
            else
            {
                TopLevelExceptionHandler.ShowError(e);
            }
        }
    }
}