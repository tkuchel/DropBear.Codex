using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class ExecutionContext
{
    private int _completedTaskCount; // Backing field for CompletedTaskCount

    public ExecutionContext(ILogger logger, ExecutionOptions options, IServiceScopeFactory scopeFactory)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ScopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public ConcurrentDictionary<string, object> Data { get; } = new(StringComparer.Ordinal);

    public ILogger Logger { get; }
    public ExecutionOptions Options { get; }
    public IServiceScopeFactory ScopeFactory { get; }

    public int TotalTaskCount { get; init; }

    /// <summary>
    /// Gets the count of completed tasks.
    /// </summary>
    public int CompletedTaskCount => _completedTaskCount; // Read-only property backed by a field

    /// <summary>
    /// Atomically increments the completed task count.
    /// </summary>
    public void IncrementCompletedTaskCount()
    {
        Interlocked.Increment(ref _completedTaskCount); // Updates the backing field
    }

    public IServiceScope CreateScope()
    {
        try
        {
            return ScopeFactory.CreateScope();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to create service scope.");
            throw;
        }
    }
}
