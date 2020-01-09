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
                    Logging.GenLog.Info(introMessage);
                    action();
                    return;
                }
                catch (Exception e)
                {
                    Logging.GenLog.Warning("Caught exception during retriable operation:");
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
    }
}