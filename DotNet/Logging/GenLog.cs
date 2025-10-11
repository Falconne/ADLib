using JetBrains.TeamCity.ServiceMessages.Write.Special;
using Serilog;

namespace ADLib.Logging;

public static class GenLog
{
    public static readonly Action<string> Debug = s => WriteToAllSinks(s, LogMessageType.Debug);

    public static readonly Action<string> Info = s => WriteToAllSinks(s, LogMessageType.Info);

    public static readonly Action<string> Warning = s => WriteToAllSinks(s, LogMessageType.Warning);

    public static readonly Action<string> Error = s => WriteToAllSinks(s, LogMessageType.Error);

    public static readonly Action<string> Fatal = s => WriteToAllSinks(s, LogMessageType.Fatal);

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

            case LogMessageType.Fatal:
                Log.Fatal(message);
                break;

            case LogMessageType.Debug:
                Log.Debug(message);
                break;
        }

        if (type == LogMessageType.Error)
        {
            WriteErrorToTeamCity(message);
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

    Fatal,

    Debug
}