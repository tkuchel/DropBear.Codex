#region

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

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

    public TaskExecutionTracker Tracker { get; }
    public ExecutionContext Context { get; }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetRequiredService<T>() where T : notnull
    {
        ThrowIfDisposed();
        return _scope.ServiceProvider.GetRequiredService<T>();
    }

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
