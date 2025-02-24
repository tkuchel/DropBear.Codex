#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Tracks execution progress, durations, and statuses of tasks in an execution engine.
/// </summary>
public sealed class TaskExecutionTracker
{
    private readonly ConcurrentDictionary<string, double> _progressCache;
    private readonly TaskExecutionStats _stats;
    private readonly ConcurrentDictionary<string, TaskExecution> _taskExecutions;
    private int _totalTaskCount;

    public TaskExecutionTracker()
    {
        _progressCache = new ConcurrentDictionary<string, double>(StringComparer.Ordinal);
        _taskExecutions = new ConcurrentDictionary<string, TaskExecution>(StringComparer.Ordinal);
        _stats = new TaskExecutionStats();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetLastProgress(string taskName)
    {
        return _progressCache.GetValueOrDefault(taskName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateLastProgress(string taskName, double progress)
    {
        _progressCache.AddOrUpdate(taskName, progress, (_, _) => progress);
    }

    /// <summary>
    ///     Marks a task as started and begins timing its execution.
    /// </summary>
    /// <param name="taskName">The name of the task.</param>
    /// <param name="task">The <see cref="Task" /> object representing the running task.</param>
    public void StartTask(string taskName, Task task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var execution = new TaskExecution(task);
        if (_taskExecutions.TryAdd(taskName, execution))
        {
            execution.Start();
        }
    }

    /// <summary>
    ///     Marks a task as complete, recording its success/failure status and duration.
    /// </summary>
    /// <param name="taskName">The name of the task.</param>
    /// <param name="success">True if the task completed successfully, otherwise false.</param>
    public void CompleteTask(string taskName, bool success)
    {
        if (_taskExecutions.TryRemove(taskName, out var execution))
        {
            execution.Complete(success);
            var duration = execution.GetDuration();
            _stats.TaskDurations.TryAdd(taskName, duration);

            if (success)
            {
                _stats.IncrementCompletedTasks();
            }
            else
            {
                _stats.IncrementFailedTasks();
            }
        }
        // *** CHANGE *** Optional: If a task is completed more than once, or never started
        // we might log or track it. But no break in the public API.
    }

    /// <summary>
    ///     Retrieves a snapshot of the current task execution statistics.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaskExecutionStats GetStats()
    {
        return _stats.Clone();
    }

    /// <summary>
    ///     Sets the total number of tasks to be executed.
    ///     Must be called before any tasks are started.
    /// </summary>
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
    ///     Resets all tracking data, discarding progress or durations for any tasks.
    /// </summary>
    public void Reset()
    {
        _progressCache.Clear();
        _taskExecutions.Clear();
        _stats.Reset();
        _totalTaskCount = 0;
    }

    /// <summary>
    ///     Waits for all currently tracked tasks to complete, or until the timeout elapses.
    /// </summary>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <returns>True if all tasks completed, false if the wait timed out.</returns>
    public async Task<bool> WaitForAllTasksToCompleteAsync(TimeSpan timeout)
    {
        var tasks = _taskExecutions.Values.Select(e => e.Task).ToArray();
        if (tasks.Length == 0)
        {
            return true;
        }

        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await Task.WhenAll(tasks).WaitAsync(cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Retrieves whether the task succeeded/failed, or <c>null</c> if not found or not completed yet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool? GetTaskStatus(string taskName)
    {
        return _taskExecutions.TryGetValue(taskName, out var execution) ? execution.Success : null;
    }

    /// <summary>
    ///     Retrieves the elapsed time for a completed task, or zero if not found.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan GetTaskDuration(string taskName)
    {
        return _stats.TaskDurations.TryGetValue(taskName, out var duration) ? duration : TimeSpan.Zero;
    }

    /// <summary>
    ///     Represents an individual task execution, tracking success/failure and elapsed time.
    /// </summary>
    private sealed class TaskExecution
    {
        private readonly Stopwatch _stopwatch;
        private int _completed;

        public TaskExecution(Task task)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            _stopwatch = new Stopwatch();
        }

        public Task Task { get; }
        public bool? Success { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start()
        {
            _stopwatch.Start();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete(bool success)
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
            {
                _stopwatch.Stop();
                Success = success;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan GetDuration()
        {
            return _stopwatch.Elapsed;
        }
    }
}
