namespace DropBear.Codex.Blazor.Enums;

/// <summary>
///     Represents the status of a progress step
/// </summary>
public enum StepStatus
{
    /// <summary>
    ///     Step has not started
    /// </summary>
    NotStarted,

    /// <summary>
    ///     Step is currently in progress
    /// </summary>
    InProgress,

    /// <summary>
    ///     Step completed successfully
    /// </summary>
    Completed,

    /// <summary>
    ///     Step completed with warnings
    /// </summary>
    Warning,

    /// <summary>
    ///     Step failed
    /// </summary>
    Failed,

    /// <summary>
    ///     Step was skipped
    /// </summary>
    Skipped
}
