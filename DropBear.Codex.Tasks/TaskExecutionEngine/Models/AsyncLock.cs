namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Asynchronous lock implementation that supports async/await patterns.
///     Provides an efficient way to control access to a shared resource in an async context.
/// </summary>
public sealed class AsyncLock : IDisposable
{
    private readonly Task<IDisposable> _releaser;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncLock" /> class.
    /// </summary>
    public AsyncLock()
    {
        _releaser = Task.FromResult((IDisposable)new Releaser(this));
    }

    /// <summary>
    ///     Disposes the AsyncLock.
    /// </summary>
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
    ///     Acquires the lock asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes with an IDisposable that releases the lock when disposed.</returns>
    public Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
    {
        var wait = _semaphore.WaitAsync(cancellationToken);

        return wait.IsCompleted
            ? _releaser
            : wait.ContinueWith(
                (_, state) => (IDisposable)state!,
                _releaser.Result,
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    /// <summary>
    ///     Helper class that releases the lock when disposed.
    /// </summary>
    private sealed class Releaser : IDisposable
    {
        private readonly AsyncLock _toRelease;
        private bool _isDisposed;

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

            _toRelease._semaphore.Release();
            _isDisposed = true;
        }
    }
}
