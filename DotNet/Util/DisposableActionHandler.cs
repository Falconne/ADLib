namespace ADLib.Util;

public class DisposableActionHandler : IDisposable
{
    public DisposableActionHandler(Action disposeAction)
    {
        _disposeAction = disposeAction;
    }

    private readonly Action _disposeAction;

    public void Dispose()
    {
        _disposeAction();
    }

    public static DisposableActionHandler CreateWithInit(
        Action initAction,
        Action disposeAction)
    {
        initAction();
        return new DisposableActionHandler(disposeAction);
    }
}