#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a simpler state object for a progress step, storing progress and status
///     without built-in locking. Raises <see cref="OnStateChanged" /> on updates.
/// </summary>
public sealed class StepState
{
    /// <summary>
    ///     Initializes a new instance of <see cref="StepState" /> with the given step ID, name, and tooltip.
    /// </summary>
    /// <param name="stepId">A unique identifier for the step.</param>
    /// <param name="name">A human-readable name for the step.</param>
    /// <param name="tooltip">Optional tooltip text displayed in the UI.</param>
    public StepState(string stepId, string name, string tooltip)
    {
        StepId = stepId;
        Name = name;
        Tooltip = tooltip;
        Progress = 0;
        Status = StepStatus.NotStarted;
    }

    /// <summary>
    ///     Gets the unique identifier for this step.
    /// </summary>
    public string StepId { get; }

    /// <summary>
    ///     Gets the name of this step, used for display.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the tooltip text for this step, if any.
    /// </summary>
    public string Tooltip { get; }

    /// <summary>
    ///     Gets the current progress (0-100).
    /// </summary>
    public double Progress { get; private set; }

    /// <summary>
    ///     Gets the current status (NotStarted, InProgress, Completed, etc.).
    /// </summary>
    public StepStatus Status { get; private set; }

    /// <summary>
    ///     Occurs when the state changes (progress or status).
    /// </summary>
    public event Action<StepState>? OnStateChanged;

    /// <summary>
    ///     Updates the progress and status, and raises <see cref="OnStateChanged" />.
    /// </summary>
    /// <param name="progress">New progress value.</param>
    /// <param name="status">New step status.</param>
    public void UpdateProgress(double progress, StepStatus status)
    {
        Progress = progress;
        Status = status;
        OnStateChanged?.Invoke(this);
    }
}
