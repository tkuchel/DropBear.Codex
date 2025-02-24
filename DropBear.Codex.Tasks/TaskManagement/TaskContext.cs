#region

using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Tasks.TaskManagement;

/// <summary>
///     Provides context information for task execution, including cancellation tokens, logging, and shared cache.
/// </summary>
public sealed class TaskContext
{
    /// <summary>
    ///     Gets the shared cache for storing and retrieving data across tasks.
    /// </summary>
    public SharedCache Cache { get; } = new();

    /// <summary>
    ///     Gets or sets the cancellation token for the current task execution.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    ///     Gets or sets the logger for logging task execution details.
    /// </summary>
    public ILogger Logger { get; set; } = LoggerFactory.Logger.ForContext<TaskContext>();
}
