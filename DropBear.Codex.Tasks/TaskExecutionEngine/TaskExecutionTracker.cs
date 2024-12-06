#region

using System.Collections.Concurrent;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Tracks the execution of tasks, including their start times, completion status, durations, and overall statistics.
/// </summary>
public sealed class TaskExecutionTracker
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, DateTime> _startTimes = new(StringComparer.Ordinal);
    private readonly TaskExecutionStats _stats = new();
    private readonly ConcurrentDictionary<string, bool> _taskStatus = new(StringComparer.Ordinal);
    private int _totalTaskCount;

    /// <summary>
    ///     Starts tracking a task by recording its start time.
    /// </summary>
    /// <param name="taskName">The name of the task to start tracking.</param>
    public void StartTask(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        _startTimes[taskName] = DateTime.UtcNow;
    }

    /// <summary>
    ///     Marks a task as completed, updates its success or failure status, and records its duration.
    /// </summary>
    /// <param name="taskName">The name of the task being completed.</param>
    /// <param name="success">Indicates whether the task completed successfully.</param>
    public void CompleteTask(string taskName, bool success)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        _taskStatus[taskName] = success;

        if (_startTimes.TryRemove(taskName, out var startTime))
        {
            _stats.TaskDurations[taskName] = DateTime.UtcNow - startTime;
        }

        lock (_lock)
        {
            if (success)
            {
                _stats.CompletedTasks++;
            }
            else
            {
                _stats.FailedTasks++;
            }
        }
    }

    /// <summary>
    ///     Gets the status of a task, indicating whether it succeeded or failed.
    /// </summary>
    /// <param name="taskName">The name of the task whose status is being queried.</param>
    /// <returns>True if the task succeeded; otherwise, false.</returns>
    public bool GetTaskStatus(string taskName)
    {
        return _taskStatus.GetValueOrDefault(taskName, false);
    }

    /// <summary>
    ///     Retrieves the current statistics for task execution, including completed and failed tasks.
    /// </summary>
    /// <returns>An instance of <see cref="TaskExecutionStats" /> containing execution statistics.</returns>
    public TaskExecutionStats GetStats()
    {
        lock (_lock)
        {
            return new TaskExecutionStats
            {
                CompletedTasks = _stats.CompletedTasks,
                FailedTasks = _stats.FailedTasks,
                SkippedTasks = _stats.SkippedTasks,
                TotalTasks = _totalTaskCount,
                TaskDurations =
                    new ConcurrentDictionary<string, TimeSpan>(_stats.TaskDurations,
                        StringComparer.OrdinalIgnoreCase)
            };
        }
    }

    /// <summary>
    ///     Sets the total number of tasks to be tracked. This value cannot be updated after tasks have started executing.
    /// </summary>
    /// <param name="count">The total number of tasks.</param>
    public void SetTotalTaskCount(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Total task count cannot be negative.");
        }

        lock (_lock)
        {
            if (_stats.CompletedTasks > 0 || _stats.FailedTasks > 0)
            {
                throw new InvalidOperationException("Cannot set total task count after tasks have started executing.");
            }

            _totalTaskCount = count;
            _stats.TotalTasks = count;
        }
    }

    /// <summary>
    ///     Resets the tracker, clearing all task-related data and statistics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _startTimes.Clear();
            _taskStatus.Clear();
            _stats.CompletedTasks = 0;
            _stats.FailedTasks = 0;
            _stats.SkippedTasks = 0;
            _stats.TaskDurations.Clear();
            _totalTaskCount = 0;
            _stats.TotalTasks = 0;
        }
    }
}
