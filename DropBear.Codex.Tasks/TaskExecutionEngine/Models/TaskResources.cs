#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Manages a set of <see cref="IAsyncDisposable" /> resources that can be added during execution
///     and disposed at the end.
/// </summary>
public sealed class TaskResources : IAsyncDisposable
{
    private readonly SemaphoreSlim _disposalLock = new(1, 1);

    private readonly ConcurrentDictionary<string, IAsyncDisposable> _resources =
        new(StringComparer.Ordinal);

    private bool _disposed;

    /// <summary>
    ///     Disposes all tracked resources asynchronously.
    /// </summary>
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

    /// <summary>
    ///     Adds (or replaces) an <see cref="IAsyncDisposable" /> resource under the given <paramref name="key" />.
    ///     If an existing resource is present, it is disposed first.
    /// </summary>
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

    /// <summary>
    ///     Gets an <see cref="IAsyncDisposable" /> resource by <paramref name="key" /> if present,
    ///     or returns <c>null</c> if not found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetResource<T>(string key) where T : class, IAsyncDisposable
    {
        return _resources.TryGetValue(key, out var resource) ? resource as T : null;
    }
}
