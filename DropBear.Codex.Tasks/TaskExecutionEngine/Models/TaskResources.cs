namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Manages resources for a task execution
/// </summary>
public sealed class TaskResources : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<IAsyncDisposable> _resources = new();
    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var resource in _resources)
            {
                try
                {
                    await resource.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Log but continue disposing other resources
                }
            }

            _resources.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask AddAsync(IAsyncDisposable resource)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TaskResources));
            }

            _resources.Add(resource);
        }
        finally
        {
            _lock.Release();
        }
    }
}
