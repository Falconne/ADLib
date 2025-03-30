namespace ADLib.Util;

/// <summary>
///     Provides an asynchronous locking mechanism using SemaphoreSlim,
///     allowing locks to be acquired asynchronously and released deterministically.
/// </summary>
public class AsyncLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    ///     Asynchronously acquires the lock.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A disposable object that releases the lock when disposed.</returns>
    public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new LockReleaser(_semaphore);
    }

    /// <summary>
    ///     Attempts to acquire the lock within the specified timeout period.
    /// </summary>
    /// <param name="timeout">The timeout period.</param>
    /// <param name="releaser">
    ///     When this method returns, contains the lock releaser if the lock was acquired, or null
    ///     if the timeout occurred.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>true if the lock was acquired; otherwise, false.</returns>
    //public async Task<bool> TryLockAsync(
    //    TimeSpan timeout,
    //    out IDisposable? releaser,
    //    CancellationToken cancellationToken = default)
    //{
    //    if (await _semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
    //    {
    //        releaser = new LockReleaser(_semaphore);
    //        return true;
    //    }

    //    releaser = null;
    //    return false;
    //}

    /// <summary>
    ///     Synchronously acquires the lock. This should be used with caution in async contexts.
    /// </summary>
    /// <returns>A disposable object that releases the lock when disposed.</returns>
    public IDisposable Lock()
    {
        _semaphore.Wait();
        return new LockReleaser(_semaphore);
    }

    /// <summary>
    ///     A disposable class that releases the semaphore when disposed.
    /// </summary>
    private sealed class LockReleaser : IDisposable
    {
        public LockReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
            _isDisposed = false;
        }

        private readonly SemaphoreSlim _semaphore;

        private bool _isDisposed;

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _semaphore.Release();
                _isDisposed = true;
            }
        }
    }
}