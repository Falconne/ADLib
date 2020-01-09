using System;
using ADLib.Logging;

namespace ADLib.Interactivity
{
    public class TemporaryConsoleInteractionHandler : ConsoleInteractionHandlerBase
    {
        internal TemporaryConsoleInteractionHandler(IInteractivityOptions options) : base(options)
        {

        }

        public override void ExitWithError(string message)
        {
            GenLog.Error(message);
            Pause();
            Environment.Exit(1);
        }

        public override void ExitWithSuccess(string message)
        {
            GenLog.Info(message);
            Pause();
            Environment.Exit(0);
        }

        private static void Pause()
        {
            GenLog.Info("Press any key to quit");
            Console.ReadKey();
        }
    }
}
