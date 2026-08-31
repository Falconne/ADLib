using System;

namespace WPFUtil;

/// <summary>
///     Single entry point for WPF application startup so that every app gets the same unhandled
///     exception wiring and logging configuration.
/// </summary>
public static class WpfAppHost
{
    /// <summary>
    ///     Attaches the top level exception handlers and starts logging. The returned handler can be
    ///     passed to <see cref="Extensions.FireAndForget" /> for startup work.
    /// </summary>
    /// <param name="beginLogging">
    ///     Logging initialiser, normally <c>ServiceConfiguration.Logging.BeginStandardLogging</c>.
    /// </param>
    public static ExceptionHandler Bootstrap(Action beginLogging)
    {
        var exceptionHandler = TopLevelExceptionHandler.Attach();
        beginLogging();
        return exceptionHandler;
    }
}
