#region

using System.Collections.Concurrent;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Represents a dependency graph for tasks, supporting dependency resolution and cycle detection.
/// </summary>
public sealed class DependencyGraph
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _dependencies = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HashSet<string>> _dependents = new(StringComparer.Ordinal);

    /// <summary>
    ///     Adds a dependency between two tasks.
    /// </summary>
    /// <param name="task">The dependent task.</param>
    /// <param name="dependency">The task it depends on.</param>
    public void AddDependency(string task, string dependency)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(dependency);

        _dependencies.GetOrAdd(task, _ => new HashSet<string>(StringComparer.Ordinal)).Add(dependency);
        _dependents.GetOrAdd(dependency, _ => new HashSet<string>(StringComparer.Ordinal)).Add(task);
    }

    /// <summary>
    ///     Checks if the graph has a circular dependency.
    /// </summary>
    public bool HasCycle()
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var recursionStack = new HashSet<string>(StringComparer.Ordinal);

        foreach (var task in _dependencies.Keys)
        {
            if (HasCycleUtil(task, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasCycleUtil(string task, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(task))
        {
            return true;
        }

        if (!visited.Add(task))
        {
            return false;
        }

        recursionStack.Add(task);

        if (_dependencies.TryGetValue(task, out var dependencies))
        {
            foreach (var dependency in dependencies)
            {
                if (HasCycleUtil(dependency, visited, recursionStack))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(task);
        return false;
    }

    /// <summary>
    ///     Retrieves the tasks that the specified task depends on.
    /// </summary>
    public IReadOnlySet<string> GetDependencies(string task)
    {
        return _dependencies.TryGetValue(task, out var deps) ? deps : new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    ///     Retrieves the tasks that depend on the specified task.
    /// </summary>
    public IReadOnlySet<string> GetDependents(string task)
    {
        return _dependents.TryGetValue(task, out var deps) ? deps : new HashSet<string>(StringComparer.Ordinal);
    }
}
