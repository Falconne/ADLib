using JetBrains.TeamCity.ServiceMessages.Write.Special;
using Serilog;

namespace ADLib.Logging;

public static class GenLog
{
    public static List<Action<LogMessageType, string>> CustomSinks { get; } = new();

    public static void Debug(string message) => WriteToAllSinks(message, LogMessageType.Debug, null);

    public static void Debug(Exception exception, string message) =>
        WriteToAllSinks(message, LogMessageType.Debug, exception);

    public static void Info(string message) => WriteToAllSinks(message, LogMessageType.Info, null);

    public static void Info(Exception exception, string message) =>
        WriteToAllSinks(message, LogMessageType.Info, exception);

    public static void Warning(string message) => WriteToAllSinks(message, LogMessageType.Warning, null);

    public static void Warning(Exception exception, string message) =>
        WriteToAllSinks(message, LogMessageType.Warning, exception);

    public static void Error(string message) => WriteToAllSinks(message, LogMessageType.Error, null);

    public static void Error(Exception exception, string message) =>
        WriteToAllSinks(message, LogMessageType.Error, exception);

    public static void Fatal(string message) => WriteToAllSinks(message, LogMessageType.Fatal, null);

    public static void Fatal(Exception exception, string message) =>
        WriteToAllSinks(message, LogMessageType.Fatal, exception);

    public static void WriteProgress(string message)
    {
        if (IsInTeamCity())
        {
            Info($"##teamcity[progressMessage '{message}']");
            return;
        }

        Info($"==== {message} ====");
    }

    private static void WriteToAllSinks(string message, LogMessageType type, Exception? exception)
    {
        var sinkMessage = exception == null
            ? message
            : $"{message}: {exception.GetType().Name}: {exception.Message}";

        foreach (var sink in CustomSinks)
        {
            sink(type, sinkMessage);
        }

        // The message is passed as a property rather than a template so that braces in paths,
        // JSON and GUIDs are not interpreted as Serilog property tokens.
        switch (type)
        {
            case LogMessageType.Info:
                Log.Information(exception, "{LogMessage}", message);
                break;

            case LogMessageType.Warning:
                Log.Warning(exception, "{LogMessage}", message);
                break;

            case LogMessageType.Error:
                Log.Error(exception, "{LogMessage}", message);
                break;

            case LogMessageType.Fatal:
                Log.Fatal(exception, "{LogMessage}", message);
                break;

            case LogMessageType.Debug:
                Log.Debug(exception, "{LogMessage}", message);
                break;
        }

        if (type == LogMessageType.Error)
        {
            WriteErrorToTeamCity(sinkMessage);
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