using JetBrains.TeamCity.ServiceMessages.Write.Special;
using Serilog;
using System.Collections.Immutable;

namespace ADLib.Logging;

public static class GenLog
{
    private static ImmutableList<Action<LogMessageType, string>> _customSinks =
        ImmutableList<Action<LogMessageType, string>>.Empty;

    public static IDisposable AddSink(Action<LogMessageType, string> sink)
    {
        ImmutableInterlocked.Update(ref _customSinks, (sinks, toAdd) => sinks.Add(toAdd), sink);
        return new SinkRegistration(sink);
    }

    public static void RemoveSink(Action<LogMessageType, string> sink)
    {
        ImmutableInterlocked.Update(ref _customSinks, (sinks, toRemove) => sinks.Remove(toRemove), sink);
    }

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

        foreach (var sink in _customSinks)
        {
            try
            {
                sink(type, sinkMessage);
            }
            catch (Exception e)
            {
                // Logged directly to avoid recursing back through the sinks
                Log.Error(e, "Custom log sink threw an exception");
            }
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

    private sealed class SinkRegistration : IDisposable
    {
        public SinkRegistration(Action<LogMessageType, string> sink)
        {
            _sink = sink;
        }

        private readonly Action<LogMessageType, string> _sink;

        public void Dispose()
        {
            RemoveSink(_sink);
        }
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