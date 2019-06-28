using System;
using System.Threading;

namespace AKBDLib.Util
{
    public static class Retry
    {
        public static void OnException(Action action, string introMessage, int numRetries = 3, int delay = 3000)
        {
            if (numRetries < 0)
                numRetries = 0;

            while (numRetries-- > 0)
            {
                try
                {
                    Logging.Wrap.Info(introMessage);
                    action();
                    return;
                }
                catch (Exception e)
                {
                    Logging.Wrap.Warning("Caught exception during retriable operation:");
                    Logging.Wrap.Warning(e.Message);
                    if (numRetries == 0)
                    {
                        Logging.Wrap.Error("No more retries left");
                        throw;
                    }
                    Logging.Wrap.Info($"Retries remaining: {numRetries}");
                    Thread.Sleep(delay);
                }
            }
        }
    }
}