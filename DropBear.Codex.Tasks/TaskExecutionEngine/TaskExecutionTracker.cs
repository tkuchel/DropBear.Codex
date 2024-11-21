#region

using System.Collections.Concurrent;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

public sealed class TaskExecutionTracker
{
    private readonly ConcurrentDictionary<string, DateTime> _startTimes = new(StringComparer.Ordinal);
    private readonly TaskExecutionStats _stats = new();
    private readonly ConcurrentDictionary<string, bool> _taskStatus = new(StringComparer.Ordinal);

    public void StartTask(string taskName)
    {
        _startTimes[taskName] = DateTime.UtcNow;
    }

    public void CompleteTask(string taskName, bool success)
    {
        _taskStatus[taskName] = success;
        if (_startTimes.TryGetValue(taskName, out var startTime))
        {
            _stats.TaskDurations[taskName] = DateTime.UtcNow - startTime;
        }

        if (success)
        {
            _stats.CompletedTasks++;
        }
        else
        {
            _stats.FailedTasks++;
        }
    }

    public bool GetTaskStatus(string taskName)
    {
        return _taskStatus.GetValueOrDefault(taskName);
    }

    public TaskExecutionStats GetStats()
    {
        return _stats;
    }
}
