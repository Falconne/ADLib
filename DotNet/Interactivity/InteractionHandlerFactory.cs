using ADLib.Logging;

namespace ADLib.Interactivity
{
    public static class InteractionHandlerFactory
    {
        public static IInteractionHandler GetHandler(IInteractivityOptions options)
        {
            if (options.Pause)
            {
                GenLog.Debug("Using TemporaryConsoleInteractionHandler");
                return new TemporaryConsoleInteractionHandler(options);
            }

            GenLog.Debug("Using ConsoleInteractionHandler");
            return new ConsoleInteractionHandler(options);
        }
    }
}