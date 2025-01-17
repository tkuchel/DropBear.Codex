namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Custom async lock implementation for more granular locking
/// </summary>
public sealed class AsyncLock : IDisposable
{
    private readonly Task<IDisposable> _releaser;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private volatile bool _isDisposed;

    public AsyncLock()
    {
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

    public Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AsyncLock));
        }

        var wait = _semaphore.WaitAsync(cancellationToken);

        return wait.IsCompleted
            ? _releaser
            : wait.ContinueWith<IDisposable>(
                (_, state) => new Releaser((AsyncLock)state!),
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
