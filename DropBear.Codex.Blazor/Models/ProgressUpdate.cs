#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the current state of the progress manager, including
///     overall progress, a message, and optional step data.
/// </summary>
public sealed record ProgressUpdate
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ProgressUpdate" /> record.
    /// </summary>
    public ProgressUpdate()
    {
        // Parameterless constructor for easy creation if needed
    }

    /// <summary>
    ///     Convenience constructor for fully specifying all update fields.
    /// </summary>
    /// <param name="isVisible">Whether the progress bar is currently visible.</param>
    /// <param name="isIndeterminate">Whether the progress is in indeterminate mode.</param>
    /// <param name="message">Message or label to display on the progress bar.</param>
    /// <param name="progress">Overall percentage progress (0-100).</param>
    /// <param name="steps">List of step configurations for stepped progress (if used).</param>
    /// <param name="stepUpdates">Recent updates to individual steps.</param>
    public ProgressUpdate(
        bool isVisible,
        bool isIndeterminate,
        string message,
        double progress,
        IReadOnlyList<ProgressStepConfig>? steps = null,
        IReadOnlyList<StepUpdate>? stepUpdates = null)
    {
        IsVisible = isVisible;
        IsIndeterminate = isIndeterminate;
        Message = message;
        Progress = progress;
        Steps = steps;
        StepUpdates = stepUpdates;
    }

    /// <summary>
    ///     Whether the progress bar is currently visible.
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    ///     Whether the progress bar is in indeterminate mode.
    /// </summary>
    public bool IsIndeterminate { get; init; }

    /// <summary>
    ///     A user-facing message or label describing the current progress/task.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     The overall progress percentage (0-100) if not in indeterminate mode.
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    ///     An optional list of step configurations if the progress is step-based.
    /// </summary>
    public IReadOnlyList<ProgressStepConfig>? Steps { get; init; }

    /// <summary>
    ///     A collection of the most recent step updates, if relevant.
    /// </summary>
    public IReadOnlyList<StepUpdate>? StepUpdates { get; init; }
}

/// <summary>
///     Represents an update for a specific step in a stepped progress workflow,
///     including its ID, progress, and status.
/// </summary>
/// <param name="StepId">The unique identifier for the step.</param>
/// <param name="Progress">Current progress (0-100) for this step.</param>
/// <param name="Status">The current <see cref="StepStatus" /> (e.g., InProgress, Completed).</param>
public sealed record StepUpdate(string StepId, double Progress, StepStatus Status);
