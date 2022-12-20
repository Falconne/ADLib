namespace ADLib.Util;

public class ThrottledObject<T> where T : notnull
{
    public readonly int MinDelayMilliseconds;

    private DateTime _lastCallTime = DateTime.MinValue;

    private readonly T _object;

    private readonly CancellationToken _cancellationToken;

    public ThrottledObject(T o, int minDelayMilliseconds, CancellationToken cancellationToken)
    {
        MinDelayMilliseconds = minDelayMilliseconds;
        _cancellationToken = cancellationToken;
        _object = o ?? throw new ArgumentNullException(nameof(o));
    }

    public async Task<T> GetAsync()
    {
        var timeSinceLastCall = (DateTime.Now - _lastCallTime).Milliseconds;
        if (timeSinceLastCall < MinDelayMilliseconds)
        {
            var sleepTime = MinDelayMilliseconds - timeSinceLastCall;
            await Task.Delay(sleepTime, _cancellationToken);
        }

        _lastCallTime = DateTime.Now;
        return _object;
    }

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