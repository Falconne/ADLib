using STLogger;
using System;
using System.Threading;

namespace Util
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
                    LogWrapper.Info(introMessage);
                    action();
                    return;
                }
                catch (Exception e)
                {
                    LogWrapper.Warning("Caught exception during retriable operation:");
                    LogWrapper.Warning(e.Message);
                    if (numRetries == 0)
                    {
                        LogWrapper.Error("No more retries left");
                        throw;
                    }
                    LogWrapper.Info($"Retries remaining: {numRetries}");
                    Thread.Sleep(delay);
                }
            }
        }
    }
}