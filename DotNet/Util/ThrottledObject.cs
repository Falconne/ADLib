namespace ADLib.Util;

public class ThrottledObject<T> where T : notnull
{
    public ThrottledObject(T o, int minDelayMilliseconds, CancellationToken cancellationToken)
    {
        _minDelayMilliseconds = minDelayMilliseconds;
        _cancellationToken = cancellationToken;
        _object = o ?? throw new ArgumentNullException(nameof(o));
    }

    private readonly int _minDelayMilliseconds;

    private readonly T _object;

    private readonly CancellationToken _cancellationToken;

    private DateTime _lastCallTime = DateTime.MinValue;

    public async Task<T> GetAsync()
    {
        // TODO: Use ThrottleService
        var timeSinceLastCall = (DateTime.Now - _lastCallTime).Milliseconds;
        if (timeSinceLastCall < _minDelayMilliseconds)
        {
            var sleepTime = _minDelayMilliseconds - timeSinceLastCall;
            await Task.Delay(sleepTime, _cancellationToken);
        }

        _lastCallTime = DateTime.Now;
        return _object;
    }

    // TODO: Sync-over-async. Use "async Task Main" in console apps and drop this wrapper.
    public T Get()
    {
        return GetAsync().Result;
    }

    public T GetWithoutThrottle()
    {
        _lastCallTime = DateTime.Now;
        return _object;
    }
}