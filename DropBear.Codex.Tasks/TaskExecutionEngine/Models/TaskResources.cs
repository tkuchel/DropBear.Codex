#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Manages resources for a task execution
/// </summary>
public sealed class TaskResources : IAsyncDisposable
{
    private readonly SemaphoreSlim _disposalLock = new(1, 1);

    private readonly ConcurrentDictionary<string, IAsyncDisposable> _resources =
        new(StringComparer.Ordinal);

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _disposalLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            var exceptions = new List<Exception>();
            foreach (var resource in _resources.Values)
            {
                try
                {
                    await resource.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            _resources.Clear();

            if (exceptions.Count > 0)
            {
                throw new AggregateException("Failed to dispose one or more resources", exceptions);
            }
        }
        finally
        {
            _disposalLock.Dispose();
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task AddResource(string key, IAsyncDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (_resources.TryGetValue(key, out var existing))
        {
            if (existing != null)
            {
                await existing.DisposeAsync().ConfigureAwait(false);
            }
        }

        _resources[key] = resource;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetResource<T>(string key) where T : class, IAsyncDisposable
    {
        return _resources.TryGetValue(key, out var resource) ? resource as T : null;
    }
}
