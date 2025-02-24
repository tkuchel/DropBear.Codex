namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     A lightweight async lock implementation wrapping a <see cref="SemaphoreSlim" />.
///     Call <see cref="LockAsync" /> to acquire, and then dispose the returned object to release.
/// </summary>
public sealed class AsyncLock : IDisposable
{
    private readonly Task<IDisposable> _releaser;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private volatile bool _isDisposed;

    public AsyncLock()
    {
        // Preallocate a releaser so we don't allocate a new one on every lock acquisition
        _releaser = Task.FromResult<IDisposable>(new Releaser(this));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _semaphore.Dispose();
    }

    /// <summary>
    ///     Acquires the lock asynchronously. The returned <see cref="IDisposable" /> must be disposed to release.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the lock has been disposed.</exception>
    public Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AsyncLock));
        }

        var waitTask = _semaphore.WaitAsync(cancellationToken);
        return waitTask.IsCompleted
            ? _releaser
            : waitTask.ContinueWith(
                (_, state) => (IDisposable)new Releaser((AsyncLock)state!),
                this,
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly AsyncLock _toRelease;
        private volatile bool _isDisposed;

        internal Releaser(AsyncLock toRelease)
        {
            _toRelease = toRelease;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (!_toRelease._isDisposed)
            {
                _toRelease._semaphore.Release();
            }
        }
    }
}
