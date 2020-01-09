using AKBDLib.Logging;

namespace AKBDLib.Util
{
    public static class WindowsHost
    {
        public static void Restart(int delay)
        {
            GenLog.Info($"Restarting computer in {delay} seconds");
            Shell.RunAndFailIfNotExitZeroMS($"shutdown /r /t {delay}");
        }
    }
}