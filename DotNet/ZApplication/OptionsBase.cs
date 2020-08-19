using ADLib.Interactivity;
using CommandLine;
using System.IO;

namespace ZApplication
{
    public class OptionsBase : IInteractivityOptions
    {
        [Option("debug", Required = false,
            HelpText = "Show debug logging")]

        public bool Debug { get; set; }


        [Option("log", Required = false,
            HelpText = "Write output to given log file as well as console")]

        public string Log
        {
            get => _log;

            set
            {
                if (File.Exists(value))
                    File.Delete(value);

                _log = value;
            }
        }


        [Option("passive", Required = false,
            HelpText = "Do not prompt, assume 'yes' or default for all prompts")]

        public bool Passive { get; set; }


        [Option("pause", Required = false,
            HelpText = "Pause before exiting")]

        public bool Pause { get; set; }


        private string _log;
    }
}