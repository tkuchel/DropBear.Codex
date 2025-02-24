#region

using System.Collections.Concurrent;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Tracks high-level statistics of task execution, such as counts of completed, failed, or skipped tasks.
/// </summary>
public sealed class TaskExecutionStats
{
    private int _completedTasks;
    private int _failedTasks;
    private int _skippedTasks;

    public int CompletedTasks
    {
        get => _completedTasks;
        init => _completedTasks = value;
    }

    public int FailedTasks
    {
        get => _failedTasks;
        init => _failedTasks = value;
    }

    public int SkippedTasks
    {
        get => _skippedTasks;
        init => _skippedTasks = value;
    }

    /// <summary>
    ///     Total tasks planned for execution.
    /// </summary>
    public int TotalTasks { get; set; }

    /// <summary>
    ///     Stores individual task durations by name.
    /// </summary>
    public ConcurrentDictionary<string, TimeSpan> TaskDurations { get; init; } =
        new(StringComparer.Ordinal);

    public void IncrementCompletedTasks()
    {
        Interlocked.Increment(ref _completedTasks);
    }

    public void IncrementFailedTasks()
    {
        Interlocked.Increment(ref _failedTasks);
    }

    public void IncrementSkippedTasks()
    {
        Interlocked.Increment(ref _skippedTasks);
    }

    /// <summary>
    ///     Resets all statistics to zero/empty.
    /// </summary>
    public void Reset()
    {
        _completedTasks = 0;
        _failedTasks = 0;
        _skippedTasks = 0;
        TotalTasks = 0;
        TaskDurations.Clear();
    }

    /// <summary>
    ///     Creates a shallow copy of the stats, including a clone of <see cref="TaskDurations" />.
    /// </summary>
    public TaskExecutionStats Clone()
    {
        return new TaskExecutionStats
        {
            _completedTasks = _completedTasks,
            _failedTasks = _failedTasks,
            _skippedTasks = _skippedTasks,
            TotalTasks = TotalTasks,
            TaskDurations = new ConcurrentDictionary<string, TimeSpan>(TaskDurations, StringComparer.Ordinal)
        };
    }
}
