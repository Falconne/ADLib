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
        using var process = Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
        if (process == null)
        {
            GenLog.Warning($"No process was started for: {uri}");
        }
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
        var processes = Process.GetProcessesByName(name);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            DisposeAll(processes);
        }
    }

    public static void KillApp(string name)
    {
        var processes = Process.GetProcessesByName(name);
        try
        {
            var running = processes.FirstOrDefault();
            if (running == null)
            {
                return;
            }

            GenLog.Info($"Killing app {name}");
            try
            {
                running.Kill();
            }
            catch (InvalidOperationException e)
            {
                GenLog.Warning(e, $"App has already exited: {name}");
            }
        }
        finally
        {
            DisposeAll(processes);
        }
    }

    private static void DisposeAll(Process[] processes)
    {
        foreach (var process in processes)
        {
            process.Dispose();
        }
    }
}