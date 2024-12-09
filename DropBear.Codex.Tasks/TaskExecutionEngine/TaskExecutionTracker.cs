﻿#region

using System.Collections.Concurrent;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Tracks the execution of tasks, including their start times, completion status, durations, and overall statistics.
///     Thread-safe implementation with minimized locking and performance optimizations.
/// </summary>
public sealed class TaskExecutionTracker
{
    private readonly ConcurrentDictionary<string, DateTime> _startTimes = new(StringComparer.Ordinal);
    private readonly TaskExecutionStats _stats = new();
    private readonly ConcurrentDictionary<string, bool> _taskStatus = new(StringComparer.Ordinal);
    private int _totalTaskCount;

    /// <summary>
    ///     Starts tracking a task by recording its start time.
    /// </summary>
    /// <param name="taskName">The name of the task to start tracking.</param>
    /// <exception cref="ArgumentException">Thrown if the task name is null or empty.</exception>
    public void StartTask(string taskName)
    {
        ArgumentNullException.ThrowIfNull(taskName);
        _startTimes[taskName] = DateTime.UtcNow;
    }

    /// <summary>
    ///     Marks a task as completed, updates its success or failure status, and records its duration.
    /// </summary>
    /// <param name="taskName">The name of the task being completed.</param>
    /// <param name="success">Indicates whether the task completed successfully.</param>
    public void CompleteTask(string taskName, bool success)
    {
        ArgumentNullException.ThrowIfNull(taskName);
        _taskStatus[taskName] = success;

        if (_startTimes.TryRemove(taskName, out var startTime))
        {
            _stats.TaskDurations[taskName] = DateTime.UtcNow - startTime;
        }

        if (success)
        {
            _stats.IncrementCompletedTasks();
        }
        else
        {
            _stats.IncrementFailedTasks();
        }
    }


    /// <summary>
    ///     Retrieves the current statistics for task execution, including completed and failed tasks.
    /// </summary>
    public TaskExecutionStats GetStats()
    {
        return new TaskExecutionStats
        {
            CompletedTasks = _stats.CompletedTasks,
            FailedTasks = _stats.FailedTasks,
            SkippedTasks = _stats.SkippedTasks,
            TotalTasks = _totalTaskCount,
            TaskDurations = new ConcurrentDictionary<string, TimeSpan>(_stats.TaskDurations, StringComparer.Ordinal)
        };
    }

    /// <summary>
    ///     Sets the total number of tasks to be tracked. This value cannot be updated after tasks have started executing.
    /// </summary>
    /// <param name="count">The total number of tasks.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the count is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown if tasks have already started executing.</exception>
    public void SetTotalTaskCount(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Total task count cannot be negative.");
        }

        if (_stats.CompletedTasks > 0 || _stats.FailedTasks > 0)
        {
            throw new InvalidOperationException("Cannot set total task count after tasks have started executing.");
        }

        _totalTaskCount = count;
        _stats.TotalTasks = count;
    }

    /// <summary>
    ///     Resets the tracker, clearing all task-related data and statistics.
    /// </summary>
    public void Reset()
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
