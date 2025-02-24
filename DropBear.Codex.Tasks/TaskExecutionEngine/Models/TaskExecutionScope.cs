#region

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Represents an isolated scope for task execution, including DI services,
///     a <see cref="TaskExecutionTracker" />, and an <see cref="ExecutionContext" />.
/// </summary>
public sealed class TaskExecutionScope : IDisposable
{
    private readonly ILogger _logger;
    private readonly IServiceScope _scope;
    private volatile bool _disposed;

    public TaskExecutionScope(
        IServiceScope scope,
        TaskExecutionTracker tracker,
        ExecutionContext context,
        ILogger logger)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Tracks progress, durations, and statuses of tasks in this scope.
    /// </summary>
    public TaskExecutionTracker Tracker { get; }

    /// <summary>
    ///     Holds shared resources and options for the current execution context.
    /// </summary>
    public ExecutionContext Context { get; }

    /// <summary>
    ///     Disposes the underlying scope and marks this object as disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _scope.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disposing TaskExecutionScope");
        }
    }

    /// <summary>
    ///     Retrieves a required service from the DI scope.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetRequiredService<T>() where T : notnull
    {
        ThrowIfDisposed();
        return _scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    ///     Retrieves a required service from the DI scope by type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object GetRequiredService(Type serviceType)
    {
        ThrowIfDisposed();
        return _scope.ServiceProvider.GetRequiredService(serviceType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TaskExecutionScope));
        }
    }
}
