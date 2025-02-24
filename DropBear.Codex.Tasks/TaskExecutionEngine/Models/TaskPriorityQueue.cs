#region

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     A priority-based queue for <see cref="ITask" />, using multiple <see cref="ConcurrentQueue{ITask}" />.
///     Tasks are dequeued from the highest priority first.
/// </summary>
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

        // Sort priorities descending
        _priorityLevels = Enum.GetValues<TaskPriority>()
            .OrderByDescending(p => (int)p)
            .ToArray();

        foreach (var priority in _priorityLevels)
        {
            _priorityQueues[priority] = new ConcurrentQueue<ITask>();
        }
    }

    /// <summary>
    ///     Current total count of tasks in all queues.
    /// </summary>
    public long Count => Interlocked.Read(ref _count);

    /// <summary>
    ///     Indicates whether there are any tasks in the queue.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    ///     Enqueues a task according to its priority.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(ITask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        _priorityQueues[task.Priority].Enqueue(task);
        _taskPriorities.TryAdd(task.Name, task.Priority);
        Interlocked.Increment(ref _count);
    }

    /// <summary>
    ///     Dequeues the highest priority task available, or returns false if empty.
    /// </summary>
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

    /// <summary>
    ///     Peeks at the highest priority task, without removing it. Returns false if empty.
    /// </summary>
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

    /// <summary>
    ///     Clears all tasks from all priority queues.
    /// </summary>
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

    /// <summary>
    ///     Returns the number of tasks in the queue for a specific <paramref name="priority" />.
    /// </summary>
    public int GetQueueLength(TaskPriority priority)
    {
        return _priorityQueues.TryGetValue(priority, out var queue)
            ? queue.Count
            : 0;
    }

    /// <summary>
    ///     Returns a dictionary of queue lengths keyed by priority level.
    /// </summary>
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
