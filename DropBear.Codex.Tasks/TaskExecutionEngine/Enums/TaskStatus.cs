namespace DropBear.Codex.Tasks.TaskExecutionEngine.Enums;

/// <summary>
///     Represents the status of a task during its execution lifecycle.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    ///     The task has not started execution yet.
    /// </summary>
    NotStarted,

    /// <summary>
    ///     The task is currently in progress.
    /// </summary>
    InProgress,

    /// <summary>
    ///     The task completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    ///     The task execution failed.
    /// </summary>
    Failed,

    /// <summary>
    ///     The task execution was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    ///     The task was skipped due to unmet conditions.
    /// </summary>
    Skipped
}
