#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Resolves task dependencies and provides a topological order of tasks for execution.
/// </summary>
public sealed class TaskDependencyResolver
{
    private readonly Dictionary<string, HashSet<string>> _graph;
    private readonly Dictionary<string, int> _inDegree;
    private readonly ObjectPool<List<ITask>> _listPool;
    private readonly ObjectPool<HashSet<string>> _setPool;

    public TaskDependencyResolver(ObjectPool<HashSet<string>> setPool, ObjectPool<List<ITask>> listPool)
    {
        _graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        _inDegree = new Dictionary<string, int>(StringComparer.Ordinal);
        _setPool = setPool;
        _listPool = listPool;
    }

    /// <summary>
    ///     Attempts to resolve dependencies among tasks, returning them in a valid execution order.
    /// </summary>
    /// <param name="tasks">All tasks keyed by name.</param>
    /// <param name="sortedTasks">If successful, a list of tasks in topologically sorted order.</param>
    /// <returns>A success or failure result with a relevant <see cref="TaskExecutionError" />.</returns>
    public Result<Unit, TaskExecutionError> ResolveDependencies(
        IReadOnlyDictionary<string, ITask> tasks,
        out List<ITask>? sortedTasks)
    {
        sortedTasks = _listPool.Get(); // Initialize the out parameter

        // *** CHANGE *** Short-circuit if no tasks
        if (tasks.Count == 0)
        {
            return Result<Unit, TaskExecutionError>.Success(Unit.Value);
        }

        try
        {
            BuildGraph(tasks);
            var sortResult = TopologicalSort();

            if (!sortResult.IsSuccess)
            {
                _listPool.Return(sortedTasks);
                sortedTasks = new List<ITask>();
                return Result<Unit, TaskExecutionError>.Failure(sortResult.Error);
            }

            foreach (var name in sortResult.Value)
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
            _listPool.Return(sortedTasks);
            sortedTasks = new List<ITask>();

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

    private Result<List<string>, TaskExecutionError> TopologicalSort()
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
            // Circular dependency detected - identify the tasks involved
            var unprocessedTasks = _graph.Keys.Except(result).ToList();
            var message = $"Circular dependency detected among tasks: {string.Join(", ", unprocessedTasks)}";
            return Result<List<string>, TaskExecutionError>.Failure(
                new TaskExecutionError(message));
        }

        return Result<List<string>, TaskExecutionError>.Success(result);
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
