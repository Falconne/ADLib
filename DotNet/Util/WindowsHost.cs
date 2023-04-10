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

    public static void OpenInDefaultBrowser(string url)
    {
        GenLog.Info($"Opening in browser: {url}");
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}