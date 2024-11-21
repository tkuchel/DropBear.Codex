namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

public sealed class DependencyGraph
{
    private readonly Dictionary<string, HashSet<string>> _dependencies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _dependents = new(StringComparer.Ordinal);

    public void AddDependency(string task, string dependency)
    {
        if (!_dependencies.TryGetValue(task, out var deps))
        {
            deps = new HashSet<string>(StringComparer.Ordinal);
            _dependencies[task] = deps;
        }

        deps.Add(dependency);

        if (!_dependents.TryGetValue(dependency, out var dependents))
        {
            dependents = new HashSet<string>(StringComparer.Ordinal);
            _dependents[dependency] = dependents;
        }

        dependents.Add(task);
    }

    // Alternative more concise version using the null coalescing operator
    public void AddDependency2(string task, string dependency)
    {
        (_dependencies[task] ??= new HashSet<string>(StringComparer.Ordinal)).Add(dependency);
        (_dependents[dependency] ??= new HashSet<string>(StringComparer.Ordinal)).Add(task);
    }

    public bool HasCycle()
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

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

        if (visited.Contains(task))
        {
            return false;
        }

        visited.Add(task);
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

    // Additional helper methods that could be useful
    public IReadOnlySet<string> GetDependencies(string task)
    {
        return _dependencies.TryGetValue(task, out HashSet<string>? deps) ? deps : new HashSet<string>();
    }

    public IReadOnlySet<string> GetDependents(string task)
    {
        return _dependents.TryGetValue(task, out HashSet<string>? deps) ? deps : new HashSet<string>();
    }

    public bool HasDependencies(string task)
    {
        return _dependencies.ContainsKey(task) && _dependencies[task].Count > 0;
    }

    public bool HasDependents(string task)
    {
        return _dependents.ContainsKey(task) && _dependents[task].Count > 0;
    }
}
