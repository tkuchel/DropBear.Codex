#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Holds data and services related to the current task execution environment.
/// </summary>
public sealed partial class ExecutionContext
{
    private readonly ConcurrentDictionary<string, object?> _sharedResources;
    private int _completedTaskCount;

    /// <summary>
    ///     Initializes an <see cref="ExecutionContext" /> with the specified options and scope factory.
    /// </summary>
    public ExecutionContext(ExecutionOptions options, IServiceScopeFactory scopeFactory, ILogger<ExecutionContext> logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ScopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _sharedResources = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
    }

    /// <summary>
    ///     Gets the logger for the execution context.
    /// </summary>
    public ILogger<ExecutionContext> Logger { get; }

    /// <summary>
    ///     Gets the execution options being used.
    /// </summary>
    public ExecutionOptions Options { get; }

    /// <summary>
    ///     Allows creation of scoped service providers for tasks that need DI services.
    /// </summary>
    public IServiceScopeFactory ScopeFactory { get; }

    /// <summary>
    ///     The total number of tasks in this execution, used for progress calculations.
    /// </summary>
    public int TotalTaskCount { get; set; }

    /// <summary>
    ///     Optional general resources or states that tasks may refer to.
    /// </summary>
    public TaskResources? Resources { get; set; }

    /// <summary>
    ///     Retrieves a shared resource by string key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetResource<T>(string key) where T : class
    {
        return _sharedResources.TryGetValue(key, out var resource) ? resource as T : null;
    }

    /// <summary>
    ///     Sets or removes a shared resource by string key.
    /// </summary>
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

    /// <summary>
    ///     Gets the count of tasks that have completed so far (success or failure).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCompletedTaskCount()
    {
        return Volatile.Read(ref _completedTaskCount);
    }

    /// <summary>
    ///     Increments the count of tasks that have completed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementCompletedTaskCount()
    {
        Interlocked.Increment(ref _completedTaskCount);
    }

    /// <summary>
    ///     Creates a new DI scope asynchronously, yielding the current thread first to avoid blocking.
    /// </summary>
    public async ValueTask<IServiceScope> CreateScopeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Yield(); // ensures not blocking the current thread
            return ScopeFactory.CreateScope();
        }
        catch (Exception ex)
        {
            LogFailedToCreateServiceScope(Logger, ex);
            throw;
        }
    }

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to create service scope")]
    static partial void LogFailedToCreateServiceScope(ILogger<ExecutionContext> logger, Exception ex);

    #endregion
}
