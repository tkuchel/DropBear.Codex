#region

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class TaskPriorityQueue
{
    private readonly TaskPriority[] _priorityLevels;
    private readonly ConcurrentDictionary<TaskPriority, ConcurrentQueue<ITask>> _priorityQueues;
    private readonly ConcurrentDictionary<string, TaskPriority> _taskPriorities;
    private long _count;

    public TaskPriorityQueue()
    {
        _priorityQueues = new ConcurrentDictionary<TaskPriority, ConcurrentQueue<ITask>>();
        _taskPriorities = new ConcurrentDictionary<string, TaskPriority>(StringComparer.Ordinal);

        // Get and sort priority levels
        _priorityLevels = Enum.GetValues<TaskPriority>()
            .OrderByDescending(p => (int)p)
            .ToArray();

        // Initialize queues for all priority levels
        foreach (var priority in _priorityLevels)
        {
            _priorityQueues[priority] = new ConcurrentQueue<ITask>();
        }
    }

    public long Count => Interlocked.Read(ref _count);
    public bool IsEmpty => Count == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(ITask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        _priorityQueues[task.Priority].Enqueue(task);
        _taskPriorities.TryAdd(task.Name, task.Priority);
        Interlocked.Increment(ref _count);
    }

    public bool TryDequeue([NotNullWhen(true)] out ITask? task)
    {
        task = null;

        foreach (var priority in _priorityLevels)
        {
            var queue = _priorityQueues[priority];
            if (queue.TryDequeue(out task))
            {
                _taskPriorities.TryRemove(task.Name, out _);
                Interlocked.Decrement(ref _count);
                return true;
            }
        }

        return false;
    }

    public bool TryPeek([NotNullWhen(true)] out ITask? task)
    {
        task = null;

        foreach (var priority in _priorityLevels)
        {
            var queue = _priorityQueues[priority];
            if (queue.TryPeek(out task))
            {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        foreach (var queue in _priorityQueues.Values)
        {
            while (queue.TryDequeue(out _)) { }
        }

        _taskPriorities.Clear();
        Interlocked.Exchange(ref _count, 0);
    }

    public int GetQueueLength(TaskPriority priority)
    {
        return _priorityQueues.TryGetValue(priority, out var queue)
            ? queue.Count
            : 0;
    }

    public IDictionary<TaskPriority, int> GetQueueLengths()
    {
        var lengths = new Dictionary<TaskPriority, int>();
        foreach (var priority in _priorityLevels)
        {
            lengths[priority] = GetQueueLength(priority);
        }

        return lengths;
    }
}
