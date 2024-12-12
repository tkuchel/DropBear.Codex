#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the current state of progress for a step
/// </summary>
public sealed class StepProgressState
{
    private volatile bool _hasStarted;
    private double _progress;
    private volatile StepStatus _status;

    /// <summary>
    ///     Creates a new instance of StepProgressState
    /// </summary>
    public StepProgressState()
    {
        StartTime = DateTimeOffset.UtcNow;
        LastUpdateTime = StartTime;
    }

    /// <summary>
    ///     Gets or sets the current progress (0-100)
    /// </summary>
    public double Progress
    {
        get => _progress;
        set => _progress = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    ///     Gets or sets the current status of the step
    /// </summary>
    public StepStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            if (value == StepStatus.InProgress && !_hasStarted)
            {
                _hasStarted = true;
                StartTime = DateTimeOffset.UtcNow;
            }
        }
    }

    /// <summary>
    ///     Gets the time when this step started
    /// </summary>
    public DateTimeOffset StartTime { get; private set; }

    /// <summary>
    ///     Gets the time of the last progress update
    /// </summary>
    public DateTimeOffset LastUpdateTime { get; private set; }

    /// <summary>
    ///     Gets the lock for thread-safe updates
    /// </summary>
    public SemaphoreSlim UpdateLock { get; } = new(1, 1);

    /// <summary>
    ///     Gets how long this step has been running
    /// </summary>
    public TimeSpan RunningTime => _hasStarted ? DateTimeOffset.UtcNow - StartTime : TimeSpan.Zero;

    /// <summary>
    ///     Updates the progress with proper locking
    /// </summary>
    public async Task UpdateProgressAsync(double newProgress, StepStatus newStatus)
    {
        await UpdateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Progress = newProgress;
            Status = newStatus;
            LastUpdateTime = DateTimeOffset.UtcNow;
        }
        finally
        {
            UpdateLock.Release();
        }
    }
}
