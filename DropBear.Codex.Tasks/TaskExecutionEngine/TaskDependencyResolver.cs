#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

public sealed class TaskDependencyResolver
{
    private readonly Dictionary<string, HashSet<string>> _graph;
    private readonly Dictionary<string, int> _inDegree;
    private readonly ObjectPool<List<ITask>> _ListPool;
    private readonly ObjectPool<HashSet<string>> _setPool;

    public TaskDependencyResolver(ObjectPool<HashSet<string>> setPool, ObjectPool<List<ITask>> ListPool)
    {
        _graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        _inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        _setPool = setPool;
        _ListPool = ListPool;
    }

    public Result<Unit, TaskExecutionError> ResolveDependencies(
        IReadOnlyDictionary<string, ITask> tasks,
        out List<ITask>? sortedTasks)
    {
        sortedTasks = _ListPool.Get(); // Initialize the out parameter

        try
        {
            BuildGraph(tasks);
            var sorted = TopologicalSort();

            foreach (var name in sorted)
            {
                if (tasks.TryGetValue(name, out var task))
                {
                    sortedTasks.Add(task);
                }
            }

            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _ListPool.Return(sortedTasks); // Return the List to pool on failure
            sortedTasks = new List<ITask>(); // Ensure out parameter is assigned

            return Result<Unit, TaskExecutionError>.Failure(
                new TaskExecutionError("Failed to resolve dependencies", null, ex));
        }
        finally
        {
            ClearGraph();
        }
    }

    private void BuildGraph(IReadOnlyDictionary<string, ITask> tasks)
    {
        foreach (var task in tasks.Values)
        {
            if (!_graph.ContainsKey(task.Name))
            {
                _graph[task.Name] = _setPool.Get();
                _inDegree[task.Name] = 0;
            }

            foreach (var dep in task.Dependencies)
            {
                if (!_graph.ContainsKey(dep))
                {
                    _graph[dep] = _setPool.Get();
                    _inDegree[dep] = 0;
                }

                _graph[dep].Add(task.Name);
                _inDegree[task.Name]++;
            }
        }
    }

    private List<string> TopologicalSort()
    {
        var result = new List<string>(_graph.Count);
        var queue = new Queue<string>();

        // Find all nodes with no dependencies
        foreach (var kvp in _inDegree.Where(x => x.Value == 0))
        {
            queue.Enqueue(kvp.Key);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var dependent in _graph[current])
            {
                _inDegree[dependent]--;
                if (_inDegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        if (result.Count != _graph.Count)
        {
            throw new InvalidOperationException("Circular dependency detected");
        }

        return result;
    }


    private void ClearGraph()
    {
        foreach (var set in _graph.Values)
        {
            _setPool.Return(set);
        }

        _graph.Clear();
        _inDegree.Clear();
    }
}
