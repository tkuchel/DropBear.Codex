#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the current state of the progress manager.
/// </summary>
public sealed record ProgressUpdate
{
    // Optional parameterless constructor if you want to create blank records easily
    public ProgressUpdate() { }

    // Example convenience constructor:
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
    ///     Whether the progress bar is in indeterminate mode (i.e. unknown completion time).
    /// </summary>
    public bool IsIndeterminate { get; init; }

    /// <summary>
    ///     Message or label to display on the progress bar.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     Overall percentage progress in normal mode (0-100).
    /// </summary>
    public double Progress { get; init; }

    /// <summary>
    ///     If in stepped mode, this list defines each step in the progress workflow.
    /// </summary>
    public IReadOnlyList<ProgressStepConfig>? Steps { get; init; }

    /// <summary>
    ///     The most recent step updates, if relevant.
    /// </summary>
    public IReadOnlyList<StepUpdate>? StepUpdates { get; init; }
}

/// <summary>
///     Represents a single step update: which step, how much progress, and its status.
/// </summary>
public sealed record StepUpdate(string StepId, double Progress, StepStatus Status);
