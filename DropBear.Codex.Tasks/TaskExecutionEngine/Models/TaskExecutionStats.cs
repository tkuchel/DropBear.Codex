#region

using System.Collections.Concurrent;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class TaskExecutionStats
{
    private int _completedTasks;
    private int _failedTasks;
    private int _skippedTasks;

    /// <summary>
    ///     Gets or sets the total number of tasks to execute.
    /// </summary>
    public int TotalTasks
    {
        get;
        set;
        // Direct assignment as atomic updates are not required here
    }

    /// <summary>
    ///     Gets or sets the total number of completed tasks.
    ///     Atomic updates use the private backing field.
    /// </summary>
    public int CompletedTasks
    {
        get => _completedTasks;
        set => _completedTasks = value; // Direct assignment for non-atomic use cases
    }

    /// <summary>
    ///     Gets or sets the total number of failed tasks.
    ///     Atomic updates use the private backing field.
    /// </summary>
    public int FailedTasks
    {
        get => _failedTasks;
        set => _failedTasks = value; // Direct assignment for non-atomic use cases
    }

    /// <summary>
    ///     Gets or sets the total number of skipped tasks.
    /// </summary>
    public int SkippedTasks
    {
        get => _skippedTasks;
        set => _skippedTasks = value;
    }

    /// <summary>
    ///     A dictionary tracking task durations.
    /// </summary>
    public ConcurrentDictionary<string, TimeSpan> TaskDurations { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    ///     Atomically increments the total number of completed tasks.
    /// </summary>
    public void IncrementCompletedTasks()
    {
        Interlocked.Increment(ref _completedTasks);
    }

    /// <summary>
    ///     Atomically increments the total number of failed tasks.
    /// </summary>
    public void IncrementFailedTasks()
    {
        Interlocked.Increment(ref _failedTasks);
    }

    /// <summary>
    ///     Atomically increments the total number of skipped tasks.
    /// </summary>
    public void IncrementSkippedTasks()
    {
        Interlocked.Increment(ref _skippedTasks);
    }
}
