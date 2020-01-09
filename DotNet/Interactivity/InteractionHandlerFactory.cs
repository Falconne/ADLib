using ADLib.Logging;
using System;

namespace ADLib.Interactivity
{
    public static class InteractionHandlerFactory
    {
        public static IInteractionHandler GetHandler(IInteractivityOptions options)
        {
            if ("1" == Environment.GetEnvironmentVariable("ZInteractive"))
            {
                GenLog.Debug("Using TemporaryConsoleInteractionHandler");
                return new TemporaryConsoleInteractionHandler(options);
            }

            GenLog.Debug("Using ConsoleInteractionHandler");
            return new ConsoleInteractionHandler(options);
        }
    }
}