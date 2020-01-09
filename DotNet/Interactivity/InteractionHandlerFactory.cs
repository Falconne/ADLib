using AKBDLib.Logging;
using AKBDLib.Util;

namespace AKBDLib.Interactivity
{
    public static class InteractionHandlerFactory
    {
        public static IInteractionHandler GetHandler(IInteractivityOptions options)
        {
            if (Shell.IsEnvironmentVariableTrue("ZInteractive"))
            {
                GenLog.Debug("Using TemporaryConsoleInteractionHandler");
                return new TemporaryConsoleInteractionHandler(options);
            }

            GenLog.Debug("Using ConsoleInteractionHandler");
            return new ConsoleInteractionHandler(options);
        }
    }
}