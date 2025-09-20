using ADLib.Exceptions;
using ADLib.Logging;

namespace ADLib.Util;

public static class Retry
{
    public static async Task OnExceptionAsync(
        Func<Task> action,
        string? introMessage,
        int numRetries = 3,
        int delay = 3000)
    {
        await OnExceptionAsync(action, introMessage, CancellationToken.None, numRetries, delay)
            .ConfigureAwait(false);
    }

    // TODO reorder and use default cancellation token
    public static async Task OnExceptionAsync(
        Func<Task> action,
        string? introMessage,
        CancellationToken cancellationToken,
        int numRetries = 3,
        int delay = 3000)

    {
        if (numRetries < 0)
        {
            numRetries = 0;
        }

        while (numRetries-- > 0 && !cancellationToken.IsCancellationRequested)
        {
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
                GenLog.Error(
                    $"Aborting due to fatal exception: {e?.InnerException?.GetType()}: {e?.InnerException?.Message}");

                throw;
            }
            catch (Exception e)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    GenLog.Warning("Cancelling retry-able operation");
                    throw;
                }

                GenLog.Warning(
                    $"Caught exception during retry-able operation ({introMessage}): {e.GetType()}");

                GenLog.Warning(e.Message);
                if (numRetries == 0)
                {
                    GenLog.Error("No more retries left");
                    throw;
                }

                GenLog.Info($"Retries remaining: {numRetries}");
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay *= 2;
            }
        }
    }

    public static void OnException(Action action, string introMessage, int numRetries = 3, int delay = 3000)

    {
        OnExceptionAsync(() => Task.Run(action), introMessage, CancellationToken.None, numRetries, delay)
            .Wait();
    }
}