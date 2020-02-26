using System;

namespace ADLib.Logging
{
    public static class GenLog
    {
        public static Action<string> Debug = s => WriteToConsole(s, "DEBUG");

        public static Action<string> Info = s => WriteToConsole(s, "INFO");

        public static Action<string> Warning = s => WriteToConsole(s, "WARNING");

        public static Action<string> Error = s => WriteToConsole(s, "ERROR");


        public static void WriteAlert(string message)
        {
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(message);
            Console.ForegroundColor = previous;
        }

        private static void WriteToConsole(string message, string type)
        {
            Console.WriteLine($"{type}: {message}");
        }
    }
}
