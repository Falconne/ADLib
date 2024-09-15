using JetBrains.TeamCity.ServiceMessages.Write.Special;
using Serilog;

namespace ADLib.Logging;

public static class GenLog
{
    public static bool DebugEnabled = false;

    public static string? LogFile;

    public static long MaxLogSize = 10 * 1024 * 1024;

    public static Action<string> Debug = s => WriteToAllSinks(s, LogMessageType.Debug);

    public static Action<string> Info = s => WriteToAllSinks(s, LogMessageType.Info);

    public static Action<string> Warning = s => WriteToAllSinks(s, LogMessageType.Warning);

    public static Action<string> Error = s => WriteToAllSinks(s, LogMessageType.Error);

    public static List<Action<LogMessageType, string>> CustomSinks { get; } = new();

    public static void WriteProgress(string message)
    {
        if (IsInTeamCity())
        {
            Info($"##teamcity[progressMessage '{message}']");
            return;
        }

        Info($"==== {message} ====");
    }

    private static void WriteToAllSinks(string message, LogMessageType type)
    {
        if (type == LogMessageType.Debug && !DebugEnabled)
        {
            return;
        }

        foreach (var sink in CustomSinks)
        {
            sink(type, message);
        }

        switch (type)
        {
            case LogMessageType.Info:
                Log.Information(message);
                break;

            case LogMessageType.Warning:
                Log.Warning(message);
                break;

            case LogMessageType.Error:
                Log.Error(message);
                break;

            case LogMessageType.Debug:
                Log.Debug(message);
                break;
        }

        if (type == LogMessageType.Error)
        {
            WriteErrorToTeamCity(message);
        }

        if (!IsInTeamCity())
        {
            return;
        }

        var logMessage = $"[{type.ToString().ToUpper()}]: {message}";
        WriteToFile(logMessage);
    }

    private static void WriteToFile(string logMessage)
    {
        if (string.IsNullOrWhiteSpace(LogFile))
        {
            return;
        }

        var retries = 4;
        while (true)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogFile);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Rollover existing log file if it's larger than MaxLogSize
                if (File.Exists(LogFile))
                {
                    var fi = new FileInfo(LogFile);
                    if (fi.Length > MaxLogSize)
                    {
                        var rolloverFile = Path.Combine(
                            Path.GetDirectoryName(LogFile)!,
                            $"{Path.GetFileNameWithoutExtension(LogFile)}.1{Path.GetExtension(LogFile)}");

                        if (File.Exists(rolloverFile))
                        {
                            File.Delete(rolloverFile);
                        }

                        File.Move(LogFile, rolloverFile);
                    }
                }

                var timestamp = DateTime.Now.ToString("s");
                File.AppendAllText(LogFile, $"{timestamp} {logMessage}\n");
                break;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Error writing to log file: {e.Message}");
                if (retries-- == 0)
                {
                    Console.Error.WriteLine("Giving up");
                    return;
                }

                Thread.Sleep(2000);
            }
        }
    }

    private static void SetColorForLogType(LogMessageType type)
    {
        switch (type)
        {
            case LogMessageType.Info:
                break;

            case LogMessageType.Warning:
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                break;

            case LogMessageType.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                break;

            case LogMessageType.Debug:
                Console.ForegroundColor = ConsoleColor.DarkGray;
                break;
        }
    }

    private static void WriteErrorToTeamCity(string message)
    {
        if (!IsInTeamCity())
        {
            return;
        }

        using var writer = new TeamCityServiceMessages().CreateWriter(Console.WriteLine);
        writer.WriteError(message);
    }

    private static bool IsInTeamCity()
    {
        var value = Environment.GetEnvironmentVariable("BUILD_NUMBER");
        return !string.IsNullOrWhiteSpace(value);
    }
}

public enum LogMessageType
{
    Info,

    Warning,

    Error,

    Debug
}