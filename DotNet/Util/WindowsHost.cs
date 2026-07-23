using ADLib.Exceptions;
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

    public static void OpenDir(string dir)
    {
        GenLog.Info($"Opening: {dir}");
        if (!Directory.Exists(dir))
        {
            throw new InvalidAssumptionException($"Directory not found: {dir}");
        }

        Shell.RunAndDetach("explorer.exe", dir);
    }

    public static bool IsProcessRunning(string name)
    {
        return Process.GetProcessesByName(name).Length > 0;
    }

    public static void KillApp(string name)
    {
        var result = Process.GetProcessesByName(name);
        var running = result.FirstOrDefault();
        if (running != null)
        {
            GenLog.Info($"Killing app ${name}");
            running.Kill();
        }
    }
}