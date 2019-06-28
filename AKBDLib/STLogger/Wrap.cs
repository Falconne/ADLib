using System;

namespace AKBDLib.Logging
{
    public static class Wrap
    {
        public static Action<string> Debug = s => WriteToConsole(s, "DEBUG");

        public static Action<string> Info = s => WriteToConsole(s, "INFO");

        public static Action<string> Warning = s => WriteToConsole(s, "WARNING");

        public static Action<string> Error = s => WriteToConsole(s, "ERROR");

        private static void WriteToConsole(string message, string type)
        {
            Console.WriteLine($"{type}: {message}");
        }
    }
}
