using ADLib.Logging;
using System.Diagnostics;

namespace ADLib.Util;

public static class WindowsHost
{
    public static void Restart(int delay)
    {
        GenLog.Info($"Restarting computer in {delay} seconds");
        Shell.RunAndFailIfNotExitZero($@"{Environment.SystemDirectory}\shutdown.exe", "/r", "/t", delay);
    }

    public static void RunOrOpenFile(string uri)
    {
        GenLog.Info($"Opening: {uri}");
        Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
    }

    public static bool IsProcessRunning(string name)
    {
        return Process.GetProcessesByName(name).Length > 0;
    }
}