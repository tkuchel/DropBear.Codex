using DropBear.Codex.Blazor.Enums;

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the runtime state of a progress step with optimized tracking.
/// </summary>
public sealed class ProgressStepState
{
    private StepStatus _status = StepStatus.NotStarted;
    private double _progress;
    private DateTime _lastUpdated = DateTime.UtcNow;
    private DateTime _startedAt;
    private DateTime _completedAt;

    /// <summary>
    ///     Initializes a new progress step state.
    /// </summary>
    /// <param name="stepId">The unique step identifier.</param>
    public ProgressStepState(string stepId)
    {
        ArgumentException.ThrowIfNullOrEmpty(stepId);
        StepId = stepId;
    }

    /// <summary>
    ///     Gets the unique step identifier.
    /// </summary>
    public string StepId { get; }

    /// <summary>
    ///     Gets the current progress value (0-100).
    /// </summary>
    public double Progress
    {
        get => _progress;
        private set => _progress = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    ///     Gets the current step status.
    /// </summary>
    public StepStatus Status => _status;

    /// <summary>
    ///     Gets when this step was last updated.
    /// </summary>
    public DateTime LastUpdated => _lastUpdated;

    /// <summary>
    ///     Gets when this step was started (if applicable).
    /// </summary>
    public DateTime? StartedAt => _startedAt == default ? null : _startedAt;

    /// <summary>
    ///     Gets when this step was completed (if applicable).
    /// </summary>
    public DateTime? CompletedAt => _completedAt == default ? null : _completedAt;

    /// <summary>
    ///     Gets the duration this step has been running.
    /// </summary>
    public TimeSpan Duration => _startedAt == default
        ? TimeSpan.Zero
        : (_completedAt == default ? DateTime.UtcNow : _completedAt) - _startedAt;

    /// <summary>
    ///     Gets whether this step is currently active.
    /// </summary>
    public bool IsActive => _status == StepStatus.InProgress;

    /// <summary>
    ///     Gets whether this step has completed.
    /// </summary>
    public bool IsCompleted => _status is StepStatus.Completed or StepStatus.Skipped;

    /// <summary>
    ///     Gets whether this step has failed.
    /// </summary>
    public bool HasFailed => _status == StepStatus.Failed;

    /// <summary>
    ///     Updates the progress and status of this step.
    /// </summary>
    /// <param name="progress">The new progress value.</param>
    /// <param name="status">The new status.</param>
    public void UpdateProgress(double progress, StepStatus status)
    {
        var now = DateTime.UtcNow;
        var oldStatus = _status;

        Progress = progress;
        _status = status;
        _lastUpdated = now;

        // Track timing transitions
        if (oldStatus == StepStatus.NotStarted && status == StepStatus.InProgress)
        {
            _startedAt = now;
        }
        else if (status is StepStatus.Completed or StepStatus.Failed or StepStatus.Skipped &&
                 _completedAt == default)
        {
            _completedAt = now;
        }
    }

    /// <summary>
    ///     Resets this step to its initial state.
    /// </summary>
    public void Reset()
    {
        _progress = 0;
        _status = StepStatus.NotStarted;
        _lastUpdated = DateTime.UtcNow;
        _startedAt = default;
        _completedAt = default;
    }

    /// <summary>
    ///     Creates a step update from the current state.
    /// </summary>
    /// <returns>A step update representing the current state.</returns>
    public StepUpdate ToStepUpdate() => new(StepId, Progress, Status);

    /// <summary>
    ///     Returns a string representation of this step state.
    /// </summary>
    public override string ToString() => $"{StepId}: {Progress:F1}% ({Status})";
}
