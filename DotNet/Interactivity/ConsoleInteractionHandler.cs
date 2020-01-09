using System;
using ADLib.Logging;

namespace ADLib.Interactivity
{
    public class ConsoleInteractionHandler : ConsoleInteractionHandlerBase
    {
        internal ConsoleInteractionHandler(IInteractivityOptions options) : base(options)
        {

        }

        public override void ExitWithError(string message)
        {
            GenLog.Error(message);
            Environment.Exit(1);
        }

        public override void ExitWithSuccess(string message)
        {
            GenLog.Info(message);
            Environment.Exit(0);
        }
    }
}