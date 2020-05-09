using JetBrains.TeamCity.ServiceMessages.Write.Special;
using System;
using System.IO;

namespace ADLib.Logging
{
    public static class GenLog
    {
        public static bool DebugEnabled = false;

        public static string LogFile;


        public static Action<string> Debug = s => WriteToAllSinks(s, LogMessageType.Debug);
        public static Action<string> Info = s => WriteToAllSinks(s, LogMessageType.Info);
        public static Action<string> Warning = s => WriteToAllSinks(s, LogMessageType.Warning);
        public static Action<string> Error = s => WriteToAllSinks(s, LogMessageType.Error);


        private static void WriteToAllSinks(string message, LogMessageType type)
        {
            if (type == LogMessageType.Debug && !DebugEnabled)
                return;

            var logMessage = $"[{type.ToString().ToUpper()}]: {message}";
            SetColorForLogType(type);
            Console.WriteLine(logMessage);
            Console.ResetColor();
            if (type == LogMessageType.Error)
            {
                WriteErrorToTeamCity(message);
            }

            if (!string.IsNullOrWhiteSpace(LogFile))
            {
                File.AppendAllText(LogFile, logMessage);
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
            var value = Environment.GetEnvironmentVariable("BUILD_NUMBER");
            if (string.IsNullOrWhiteSpace(value))
                // Not running in a TeamCity build
                return;

            using (var writer = new TeamCityServiceMessages().CreateWriter(Console.WriteLine))
            {
                writer.WriteError(message);
            }
        }
    }

    internal enum LogMessageType
    {
        Info,
        Warning,
        Error,
        Debug
    }
}
