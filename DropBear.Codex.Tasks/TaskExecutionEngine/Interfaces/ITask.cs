namespace DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

/// <summary>
///     Defines the contract for a task that can be executed within the execution engine.
/// </summary>
public interface ITask
{
    /// <summary>
    ///     Gets the unique name of the task.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets or sets the condition that determines whether the task should execute.
    ///     If the condition is null, the task will always execute.
    /// </summary>
    Func<ExecutionContext, bool>? Condition { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of retry attempts in case of task failure.
    /// </summary>
    int MaxRetryCount { get; set; }

    /// <summary>
    ///     Gets or sets the delay between retry attempts.
    /// </summary>
    TimeSpan RetryDelay { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to continue executing subsequent tasks even if this task fails.
    /// </summary>
    bool ContinueOnFailure { get; set; }

    /// <summary>
    ///     Gets a read-only list of task names that this task depends on.
    /// </summary>
    IReadOnlyList<string> Dependencies { get; }

    /// <summary>
    ///     Gets or sets the compensation action to execute if this task needs to be rolled back.
    /// </summary>
    Func<ExecutionContext, Task>? CompensationActionAsync { get; set; }

    /// <summary>
    ///     Validates the task's configuration and returns a value indicating whether it is valid.
    /// </summary>
    /// <returns><c>true</c> if the task is valid; otherwise, <c>false</c>.</returns>
    bool Validate();

    /// <summary>
    ///     Executes the task asynchronously within the given execution context.
    ///     Implementations should handle exceptions and log errors appropriately.
    /// </summary>
    /// <param name="context">The execution context providing shared data and services.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken);

    /// <summary>
    ///     Executes the task synchronously within the given execution context.
    ///     Implementations should handle exceptions and log errors appropriately.
    /// </summary>
    /// <param name="context">The execution context providing shared data and services.</param>
    void Execute(ExecutionContext context);

    /// <summary>
    ///     Adds a dependency to this task.
    /// </summary>
    /// <param name="dependency">The name of the task that this task depends on.</param>
    void AddDependency(string dependency);

    /// <summary>
    ///     Sets the dependencies for this task, replacing any existing dependencies.
    /// </summary>
    /// <param name="dependencies">An enumerable of task names that this task depends on.</param>
    void SetDependencies(IEnumerable<string> dependencies);
}
