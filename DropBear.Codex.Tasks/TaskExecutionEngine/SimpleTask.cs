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
    }

    /// <summary>
    ///     Gets the unique name of the task.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets or sets the condition that determines whether the task should execute.
    ///     If the condition is null, the task will always execute.
    /// </summary>
    public Func<ExecutionContext, bool>? Condition { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of retry attempts in case of task failure.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Gets or sets a value indicating whether to continue executing subsequent tasks even if this task fails.
    /// </summary>
    public bool ContinueOnFailure { get; set; } = false;

    /// <summary>
    ///     Gets a read-only list of task names that this task depends on.
    /// </summary>
    public IReadOnlyList<string> Dependencies => _dependencies.AsReadOnly();

    /// <summary>
    ///     Gets or sets the compensation action to execute if this task needs to be rolled back.
    /// </summary>
    public Func<ExecutionContext, Task>? CompensationActionAsync { get; set; }

    /// <summary>
    ///     Validates the task's configuration and returns a value indicating whether it is valid.
    /// </summary>
    /// <returns><c>true</c> if the task is valid; otherwise, <c>false</c>.</returns>
    public bool Validate()
    {
        // Add any validation logic if necessary
        return true;
    }

    /// <summary>
    ///     Executes the task asynchronously within the given execution context.
    /// </summary>
    /// <param name="context">The execution context providing shared data and services.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken)
    {
        return _executeAsync(context, cancellationToken);
    }

    /// <summary>
    ///     Executes the task synchronously within the given execution context.
    /// </summary>
    /// <param name="context">The execution context providing shared data and services.</param>
    public void Execute(ExecutionContext context)
    {
        if (_execute != null)
        {
            _execute(context);
        }
        else
        {
            // If only asynchronous execution is provided, execute synchronously
            _executeAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    ///     Adds a dependency to this task.
    /// </summary>
    /// <param name="dependency">The name of the task that this task depends on.</param>
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

    /// <summary>
    ///     Sets the dependencies for this task, replacing any existing dependencies.
    /// </summary>
    /// <param name="dependencies">An enumerable of task names that this task depends on.</param>
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
}
