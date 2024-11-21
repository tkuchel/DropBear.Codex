#region

using Microsoft.Extensions.DependencyInjection;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class TaskExecutionScope : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly IServiceScope _scope;
    private bool _disposed;

    public TaskExecutionScope(
        IServiceScope scope,
        TaskExecutionTracker tracker,
        ExecutionContext context,
        ILogger logger)
    {
        _scope = scope;
        Tracker = tracker;
        Context = context;
        _logger = logger;
    }

    public IServiceProvider ServiceProvider => _scope.ServiceProvider;
    public TaskExecutionTracker Tracker { get; }

    public ExecutionContext Context { get; }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_scope is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                _scope.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disposing task execution scope");
        }
        finally
        {
            _disposed = true;
        }
    }
}
