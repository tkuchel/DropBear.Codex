#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;
using DropBear.Codex.Tasks.TaskExecutionEngine.Enums;
using ExecutionContext = DropBear.Codex.Tasks.TaskExecutionEngine.Models.ExecutionContext;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

/// <summary>
///     Defines the contract for a task that can be executed within the execution engine.
/// </summary>
public interface ITask
{
    /// <summary>
    ///     Gets the name of the task.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     A predicate function indicating whether the task should run under the current context.
    /// </summary>
    Func<ExecutionContext, bool>? Condition { get; set; }

    /// <summary>
    ///     Maximum number of retries allowed if the task fails.
    /// </summary>
    int MaxRetryCount { get; set; }

    /// <summary>
    ///     Delay between retries.
    /// </summary>
    TimeSpan RetryDelay { get; set; }

    /// <summary>
    ///     If <c>true</c>, this task does not block subsequent tasks from running on failure.
    /// </summary>
    bool ContinueOnFailure { get; set; }

    /// <summary>
    ///     List of tasks that must be completed successfully before this task can run.
    /// </summary>
    IReadOnlyList<string> Dependencies { get; }

    /// <summary>
    ///     Estimated execution duration for informational or scheduling purposes.
    /// </summary>
    TimeSpan EstimatedDuration { get; }

    /// <summary>
    ///     The maximum time allowed for this task to run before timing out.
    /// </summary>
    TimeSpan Timeout { get; set; }

    /// <summary>
    ///     Priority level of the task.
    /// </summary>
    TaskPriority Priority { get; }

    /// <summary>
    ///     An optional compensation action that runs if the task fails and needs to roll back.
    /// </summary>
    Func<ExecutionContext, CancellationToken, Task>? CompensationActionAsync { get; set; }

    /// <summary>
    ///     A dictionary for storing extended metadata about the task.
    /// </summary>
    IDictionary<string, object> Metadata { get; }

    /// <summary>
    ///     Validates whether the task's configuration/state is valid.
    /// </summary>
    /// <returns>A result indicating whether validation succeeded or failed with details.</returns>
    Result<Unit, TaskValidationError> Validate();

    /// <summary>
    ///     Executes the task logic using the given <paramref name="context" />.
    /// </summary>
    /// <returns>A result indicating whether execution succeeded or failed with details.</returns>
    Task<Result<Unit, TaskExecutionError>> ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken);

    /// <summary>
    ///     Adds a dependency to this task.
    /// </summary>
    void AddDependency(string dependency);

    /// <summary>
    ///     Sets the dependencies for this task, replacing any existing dependencies.
    /// </summary>
    void SetDependencies(IEnumerable<string> dependencies);

    /// <summary>
    ///     Removes a dependency from this task.
    /// </summary>
    void RemoveDependency(string dependency);

    /// <summary>
    ///     Checks if the task has a specific dependency.
    /// </summary>
    bool HasDependency(string dependency);
}
