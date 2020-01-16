using System;
using ADLib.Logging;

namespace ADLib.Util
{
    public static class WindowsHost
    {
        public static void Restart(int delay)
        {
            GenLog.Info($"Restarting computer in {delay} seconds");
            Shell.RunAndFailIfNotExitZeroMS($@"{Environment.SystemDirectory}\shutdown.exe", "/r", "/t", delay);
        }
    }
}