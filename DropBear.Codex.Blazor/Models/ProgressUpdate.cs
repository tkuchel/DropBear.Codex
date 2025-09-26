#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a progress update notification with optimized equality comparison.
/// </summary>
public sealed record ProgressUpdate
{
    /// <summary>
    ///     Gets whether the progress bar is currently visible.
    /// </summary>
    public required bool IsVisible { get; init; }

    /// <summary>
    ///     Gets whether the progress is in indeterminate mode.
    /// </summary>
    public required bool IsIndeterminate { get; init; }

    /// <summary>
    ///     Gets the current progress message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     Gets the current progress value (0-100).
    /// </summary>
    public required double Progress { get; init; }

    /// <summary>
    ///     Gets the collection of progress steps, if any.
    /// </summary>
    public IReadOnlyList<ProgressStepConfig>? Steps { get; init; }

    /// <summary>
    ///     Gets the collection of step updates, if any.
    /// </summary>
    public IReadOnlyList<StepUpdate>? StepUpdates { get; init; }

    /// <summary>
    ///     Gets the timestamp of this update.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Represents an update to a specific progress step.
/// </summary>
/// <param name="StepId">The unique identifier of the step.</param>
/// <param name="Progress">The progress value (0-100).</param>
/// <param name="Status">The step status.</param>
public sealed record StepUpdate(string StepId, double Progress, StepStatus Status)
{
    /// <summary>
    ///     Gets the timestamp when this update was created.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}
