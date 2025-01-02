#region

using System.Diagnostics.CodeAnalysis;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Priority-based task queue for optimized scheduling
/// </summary>
internal sealed class TaskPriorityQueue
{
    private readonly object _lock = new();
    private readonly SortedDictionary<int, Queue<ITask>> _queues = new();

    public void Enqueue(ITask task)
    {
        lock (_lock)
        {
            var priority = (int)task.Priority;
            if (!_queues.TryGetValue(priority, out var queue))
            {
                queue = new Queue<ITask>();
                _queues[priority] = queue;
            }

            queue.Enqueue(task);
        }
    }

    public bool TryDequeue([NotNullWhen(true)] out ITask? task)
    {
        lock (_lock)
        {
            task = null;
            if (_queues.Count == 0)
            {
                return false;
            }

            var highestPriority = _queues.Keys.Max();
            var queue = _queues[highestPriority];
            if (queue.Count == 0)
            {
                _queues.Remove(highestPriority);
                return TryDequeue(out task);
            }

            task = queue.Dequeue();
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _queues.Clear();
        }
    }
}
