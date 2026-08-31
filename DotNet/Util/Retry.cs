using ADLib.Exceptions;
using ADLib.Logging;

namespace ADLib.Util;

public static class Retry
{
    public static async Task OnExceptionAsync(
        Func<Task> action,
        string? introMessage,
        int maxAttempts = 3,
        int delay = 3000)
    {
        await OnExceptionAsync(action, introMessage, CancellationToken.None, maxAttempts, delay)
            .ConfigureAwait(false);
    }

    // TODO reorder and use default cancellation token
    public static async Task OnExceptionAsync(
        Func<Task> action,
        string? introMessage,
        CancellationToken cancellationToken,
        int maxAttempts = 3,
        int delay = 3000)

    {
        if (maxAttempts < 1)
        {
            maxAttempts = 1;
        }

        for (var attempt = 1;; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!introMessage.IsEmpty())
                {
                    GenLog.Info(introMessage);
                }

                await action().ConfigureAwait(false);
                return;
            }
            catch (FatalException e)
            {
                GenLog.Error(e, "Aborting due to fatal exception");
                throw;
            }
            catch (OperationCanceledException)
            {
                GenLog.Warning("Cancelling retry-able operation");
                throw;
            }
            catch (Exception e) when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                GenLog.Warning(e, $"Attempt {attempt} of {maxAttempts} failed ({introMessage})");
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay *= 2;
            }
        }
    }

    // TODO: Sync-over-async. Use "async Task Main" in console apps and drop this wrapper.
    public static void OnException(Action action, string? introMessage, int maxAttempts = 3, int delay = 3000)

    {
        OnExceptionAsync(() => Task.Run(action), introMessage, CancellationToken.None, maxAttempts, delay)
            .Wait();
    }
}