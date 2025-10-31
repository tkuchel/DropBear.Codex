#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Represents a unified execution event for task streaming.
///     Captures all lifecycle events (started, progress, completed, failed) in a single type.
/// </summary>
public sealed record TaskExecutionEvent
{
    /// <summary>
    ///     The name of the task being executed.
    /// </summary>
    public required string TaskName { get; init; }

    /// <summary>
    ///     The type of event (Started, Progress, Completed, Failed).
    /// </summary>
    public required TaskEventType EventType { get; init; }

    /// <summary>
    ///     The current status of the task.
    /// </summary>
    public required Enums.TaskStatus Status { get; init; }

    /// <summary>
    ///     The timestamp when this event occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Progress percentage for the specific task (0-100), if applicable.
    /// </summary>
    public double? TaskProgressPercentage { get; init; }

    /// <summary>
    ///     Number of tasks completed overall, if applicable.
    /// </summary>
    public int? OverallCompletedTasks { get; init; }

    /// <summary>
    ///     Total number of tasks overall, if applicable.
    /// </summary>
    public int? OverallTotalTasks { get; init; }

    /// <summary>
    ///     Optional message describing the current state or error.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    ///     Exception details if the task failed.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    ///     Calculates overall progress percentage based on completed and total tasks.
    /// </summary>
    public double? OverallProgressPercentage =>
        OverallTotalTasks.HasValue && OverallCompletedTasks.HasValue && OverallTotalTasks.Value > 0
            ? (double)OverallCompletedTasks.Value / OverallTotalTasks.Value * 100
            : null;
}

/// <summary>
///     Represents the type of task execution event.
/// </summary>
public enum TaskEventType
{
    /// <summary>
    ///     Task execution has started.
    /// </summary>
    Started,

    /// <summary>
    ///     Task execution progress update.
    /// </summary>
    Progress,

    /// <summary>
    ///     Task execution completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    ///     Task execution failed with an error.
    /// </summary>
    Failed
}
