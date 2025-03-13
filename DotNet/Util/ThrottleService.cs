namespace ADLib.Util;

public class ThrottleService
{
    public ThrottleService(Func<string, IDisposable>? statusLogger, TimeSpan? delay = null)
    {
        _statusLogger = statusLogger;
        _delay = delay ?? TimeSpan.FromSeconds(3);
    }

    private readonly TimeSpan _delay;

    private readonly Random _random = new();

    private readonly Func<string, IDisposable>? _statusLogger;

    private DateTimeOffset _lastActionTime = DateTimeOffset.MinValue;

    public async Task ThrottleAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var nextActionTime = _lastActionTime + _delay;
        if (now < nextActionTime)
        {
            var jitter = _random.Next(2000, 4000);
            var delay = nextActionTime - now + TimeSpan.FromMilliseconds(jitter);
            using var _ = _statusLogger?.Invoke($"Delay for {delay.TotalSeconds} seconds");
            await Task.Delay(delay);
        }

        _lastActionTime = DateTimeOffset.UtcNow;
    }
}