using ADLib.Exceptions;
using ADLib.Logging;
using System;

namespace ADLib.Interactivity
{
    public abstract class ConsoleInteractionHandlerBase : IInteractionHandler
    {
        protected readonly IInteractivityOptions Options;


        protected ConsoleInteractionHandlerBase(IInteractivityOptions options)
        {
            Options = options;
            GenLog.DebugEnabled = options.Debug;
            if (!string.IsNullOrWhiteSpace(options.Log))
                GenLog.LogFile = options.Log;
        }

        public abstract void ExitWithError(string message);

        public abstract void ExitWithSuccess(string message);

        public string GetTextInput(string prompt, string defaultValue)
        {
            return GetInput(prompt, defaultValue);
        }

        public string GetTextInput(string prompt)
        {
            if (Options.Passive)
                throw new ConfigurationException($"Cannot request info without defaults in passive mode: '{prompt}'");

            while (true)
            {
                var result = GetTextInput(prompt, null);
                if (!string.IsNullOrWhiteSpace(result))
                    return result;

                GenLog.Error("Value cannot be empty");
            }
        }

        public int GetIntegerInput(string prompt, int defaultValue,
            int min = int.MinValue, int max = int.MaxValue)
        {
            while (true)
            {
                var resultString = GetInput(prompt, defaultValue.ToString());
                if (int.TryParse(resultString, out var result))
                {
                    if (result >= min && result <= max)
                        return result;
                }
                Console.Error.WriteLine($"Input must be an integer between {min} and {max}");
            }
        }

        public bool GetYesNoResponse(string prompt)
        {
            if (Options.Passive)
            {
                GenLog.Info($"Non-interactive mode, using defaults '{prompt}' => 'yes'");
                return true;
            }

            while (true)
            {
                Console.Out.Write($"{prompt} (y/n) ");
                var result = Console.ReadKey();
                Console.Out.WriteLine("");

                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (result.Key)
                {
                    case ConsoleKey.Y: return true;
                    case ConsoleKey.N: return false;
                    default: continue;
                }
            }
        }

        public bool IsPassive()
        {
            return Options.Passive;
        }

        private string GetInput(string prompt, string defaultValue)
        {
            if (Options.Passive)
            {
                GenLog.Info($"Non-interactive mode, using defaults '{prompt}' => '{defaultValue ?? "[null]"}'");
                return defaultValue;
            }

            Console.Out.Write($"{prompt} [{defaultValue ?? ""}]: ");
            var result = Console.ReadLine();
            return string.IsNullOrWhiteSpace(result) ? defaultValue : result;
        }
    }
}