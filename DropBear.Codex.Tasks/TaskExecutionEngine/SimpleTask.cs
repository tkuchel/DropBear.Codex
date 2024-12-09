#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine;

/// <summary>
///     Represents a simple task that can be executed within the execution engine.
/// </summary>
public sealed class SimpleTask : ITask
{
    private readonly List<string> _dependencies = new();
    private readonly Action<ExecutionContext>? _execute;
    private readonly Func<ExecutionContext, CancellationToken, Task> _executeAsync;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SimpleTask" /> class with asynchronous execution logic.
    /// </summary>
    /// <param name="name">The unique name of the task.</param>
    /// <param name="executeAsync">The asynchronous execution logic of the task.</param>
    public SimpleTask(string name, Func<ExecutionContext, CancellationToken, Task> executeAsync)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        Metadata = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SimpleTask" /> class with synchronous execution logic.
    /// </summary>
    /// <param name="name">The unique name of the task.</param>
    /// <param name="execute">The synchronous execution logic of the task.</param>
    public SimpleTask(string name, Action<ExecutionContext> execute)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _executeAsync = (context, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            execute(context);
            return Task.CompletedTask;
        };
        Metadata = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Func<ExecutionContext, bool>? Condition { get; set; }

    /// <inheritdoc />
    public int MaxRetryCount { get; set; } = 3;

    /// <inheritdoc />
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public bool ContinueOnFailure { get; set; } = false;

    /// <inheritdoc />
    public IReadOnlyList<string> Dependencies => _dependencies.AsReadOnly();

    /// <inheritdoc />
    public Func<ExecutionContext, CancellationToken, Task>? CompensationActionAsync { get; set; }

    /// <inheritdoc />
    public Dictionary<string, object> Metadata { get; set; }

    /// <inheritdoc />
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return false;
        }

        if (MaxRetryCount < 0)
        {
            return false;
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken)
    {
        return _executeAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public void Execute(ExecutionContext context)
    {
        if (_execute != null)
        {
            _execute(context);
        }
        else
        {
            _executeAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    /// <inheritdoc />
    public void AddDependency(string dependency)
    {
        if (string.IsNullOrWhiteSpace(dependency))
        {
            throw new ArgumentException("Dependency cannot be null or whitespace.", nameof(dependency));
        }

        if (!_dependencies.Contains(dependency, StringComparer.Ordinal))
        {
            _dependencies.Add(dependency);
        }
    }

    /// <inheritdoc />
    public void SetDependencies(IEnumerable<string> dependencies)
    {
        if (dependencies == null)
        {
            throw new ArgumentNullException(nameof(dependencies));
        }

        _dependencies.Clear();
        foreach (var dependency in dependencies)
        {
            AddDependency(dependency);
        }
    }

    /// <inheritdoc />
    public void RemoveDependency(string dependency)
    {
        if (string.IsNullOrWhiteSpace(dependency))
        {
            throw new ArgumentException("Dependency cannot be null or whitespace.", nameof(dependency));
        }

        _dependencies.Remove(dependency);
    }

    /// <inheritdoc />
    public bool HasDependency(string dependency)
    {
        if (string.IsNullOrWhiteSpace(dependency))
        {
            throw new ArgumentException("Dependency cannot be null or whitespace.", nameof(dependency));
        }

        return _dependencies.Contains(dependency, StringComparer.Ordinal);
    }
}
