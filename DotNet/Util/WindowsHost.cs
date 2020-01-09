using ADLib.Logging;

namespace ADLib.Util
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