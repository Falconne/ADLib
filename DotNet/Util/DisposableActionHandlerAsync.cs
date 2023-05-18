namespace ADLib.Util;

public class DisposableActionHandlerAsync : IAsyncDisposable
{
    public DisposableActionHandlerAsync(Func<Task> disposeAction)
    {
        _disposeAction = disposeAction;
    }

    private readonly Func<Task> _disposeAction;

    public async ValueTask DisposeAsync()
    {
        await _disposeAction();
    }

    public static async Task<DisposableActionHandlerAsync> CreateWithInit(
        Func<Task> initAction,
        Func<Task> disposeAction)
    {
        await initAction();
        return new DisposableActionHandlerAsync(disposeAction);
    }
}