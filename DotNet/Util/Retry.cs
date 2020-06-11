using JetBrains.Annotations;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ADLib.Util
{
    public static class Retry
    {
        public static async Task OnExceptionAsync(
            [InstantHandle] Func<Task> action, string introMessage, int numRetries = 3, int delay = 3000)

        {
            if (numRetries < 0)
                numRetries = 0;

            while (numRetries-- > 0)
            {
                try
                {
                    Logging.GenLog.Info(introMessage);
                    await action();
                    return;
                }
                catch (Exception e)
                {
                    Logging.GenLog.Warning("Caught exception during retry-able operation:");
                    Logging.GenLog.Warning(e.Message);
                    if (numRetries == 0)
                    {
                        Logging.GenLog.Error("No more retries left");
                        throw;
                    }
                    Logging.GenLog.Info($"Retries remaining: {numRetries}");
                    Thread.Sleep(delay);
                }
            }
        }

        public static void OnException(
            [InstantHandle] Action action, string introMessage, int numRetries = 3, int delay = 3000)

        {
            OnExceptionAsync(() => Task.Run(action), introMessage, numRetries, delay).Wait();
        }
    }
}