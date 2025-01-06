#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a thread-safe state tracking for a single progress step,
///     including start time, progress percentage, and status.
/// </summary>
public sealed class StepProgressState
{
    private volatile bool _hasStarted;
    private double _progress;
    private volatile StepStatus _status;

    /// <summary>
    ///     Creates a new instance of <see cref="StepProgressState" />,
    ///     initializing <see cref="StartTime" /> to the current UTC date/time.
    /// </summary>
    public StepProgressState()
    {
        StartTime = DateTimeOffset.UtcNow;
        LastUpdateTime = StartTime;
    }

    /// <summary>
    ///     Gets or sets the current progress percentage (0-100).
    ///     Values are clamped within this range.
    /// </summary>
    public double Progress
    {
        get => _progress;
        set => _progress = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    ///     Gets or sets the current status of this step (e.g., NotStarted, InProgress, Completed).
    ///     When set to <see cref="StepStatus.InProgress" /> for the first time, the <see cref="StartTime" /> is recorded.
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
    ///     Gets the date/time when this step started (UTC).
    ///     If never started, it's the time this object was constructed.
    /// </summary>
    private DateTimeOffset StartTime { get; set; }

    /// <summary>
    ///     Gets the date/time of the last update to progress or status.
    /// </summary>
    public DateTimeOffset LastUpdateTime { get; private set; }

    /// <summary>
    ///     A <see cref="SemaphoreSlim" /> used to synchronize updates in multi-threaded scenarios.
    /// </summary>
    public SemaphoreSlim UpdateLock { get; } = new(1, 1);

    /// <summary>
    ///     Gets how long this step has been actively running (from <see cref="StartTime" /> to now).
    ///     Returns <see cref="TimeSpan.Zero" /> if <see cref="_hasStarted" /> is false.
    /// </summary>
    public TimeSpan RunningTime => _hasStarted ? DateTimeOffset.UtcNow - StartTime : TimeSpan.Zero;

    /// <summary>
    ///     Updates the progress and status of this step in a thread-safe manner,
    ///     also refreshing <see cref="LastUpdateTime" />.
    /// </summary>
    /// <param name="newProgress">New progress value (0-100).</param>
    /// <param name="newStatus">New step status (e.g., InProgress, Completed).</param>
    /// <returns>An awaitable <see cref="Task" /> that completes when the update is done.</returns>
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
