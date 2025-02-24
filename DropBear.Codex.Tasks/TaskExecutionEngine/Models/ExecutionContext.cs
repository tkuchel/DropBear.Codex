#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class ExecutionContext
{
    private readonly object _countLock = new();
    private readonly ConcurrentDictionary<string, object?> _sharedResources;
    private int _completedTaskCount;

    public ExecutionContext(
        ExecutionOptions options,
        IServiceScopeFactory scopeFactory)
    {
        Logger = LoggerFactory.Logger.ForContext<ExecutionContext>();
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ScopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _sharedResources = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
    }

    public ILogger Logger { get; }
    public ExecutionOptions Options { get; }
    public IServiceScopeFactory ScopeFactory { get; }
    public int TotalTaskCount { get; set; }
    public TaskResources? Resources { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetResource<T>(string key) where T : class
    {
        return _sharedResources.TryGetValue(key, out var resource) ? resource as T : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetResource<T>(string key, T? resource) where T : class
    {
        if (resource == null)
        {
            _sharedResources.TryRemove(key, out _);
        }
        else
        {
            _sharedResources.AddOrUpdate(key, resource, (_, _) => resource);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCompletedTaskCount()
    {
        return Volatile.Read(ref _completedTaskCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementCompletedTaskCount()
    {
        Interlocked.Increment(ref _completedTaskCount);
    }

    public async ValueTask<IServiceScope> CreateScopeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Yield(); // Ensure we're not blocking the current thread
            return ScopeFactory.CreateScope();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to create service scope");
            throw;
        }
    }
}
