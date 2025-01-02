namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Custom async lock implementation for more granular locking
/// </summary>
public sealed class AsyncLock
{
    private readonly Task<IDisposable> _releaser;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public AsyncLock()
    {
        _releaser = Task.FromResult((IDisposable)new Releaser(this));
    }

    public Task<IDisposable> LockAsync()
    {
        var wait = _semaphore.WaitAsync();
        return wait.IsCompleted
            ? _releaser
            : wait.ContinueWith((_, state) => (IDisposable)new Releaser((AsyncLock)state!),
                this, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly AsyncLock _toRelease;

        internal Releaser(AsyncLock toRelease)
        {
            _toRelease = toRelease;
        }

        public void Dispose()
        {
            _toRelease._semaphore.Release();
        }
    }
}
