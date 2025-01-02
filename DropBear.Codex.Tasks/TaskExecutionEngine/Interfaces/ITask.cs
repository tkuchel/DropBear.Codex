using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

/// <summary>
///     Defines the contract for a task that can be executed within the execution engine.
/// </summary>
public interface ITask
{
    string Name { get; }
    Func<ExecutionContext, bool>? Condition { get; set; }
    int MaxRetryCount { get; set; }
    TimeSpan RetryDelay { get; set; }
    bool ContinueOnFailure { get; set; }
    IReadOnlyList<string> Dependencies { get; }
    TimeSpan EstimatedDuration { get; }

    TaskPriority Priority { get; }

    Func<ExecutionContext, CancellationToken, Task>?
        CompensationActionAsync { get; set; } // Updated for cancellation support

    /// <summary>
    ///     Gets or sets metadata for the task, providing additional configuration or information.
    /// </summary>
    Dictionary<string, object> Metadata { get; set; } // New extensibility point

    bool Validate();
    Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken);
 
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

    /// <summary>
    ///     Removes a dependency from this task.
    /// </summary>
    /// <param name="dependency">The name of the dependency to remove.</param>
    void RemoveDependency(string dependency); // New method

    /// <summary>
    ///     Checks if the task has a specific dependency.
    /// </summary>
    /// <param name="dependency">The name of the dependency to check.</param>
    /// <returns>True if the dependency exists; otherwise, false.</returns>
    bool HasDependency(string dependency); // New method
}
