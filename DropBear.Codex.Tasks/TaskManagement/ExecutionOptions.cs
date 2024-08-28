namespace DropBear.Codex.Tasks.TaskManagement;

/// <summary>
///     Configuration options for task execution in the TaskManager.
/// </summary>
public sealed class ExecutionOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether the task can be executed in parallel with other tasks.
    /// </summary>
    public bool AllowParallel { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of retry attempts for a task in case of failure.
    /// </summary>
    public int MaxRetries { get; set; } = 3; // Set a default retry count

    /// <summary>
    ///     Gets or sets the delay between retry attempts in case of failure.
    ///     Default value is 1 second.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Gets or sets the timeout for executing the task.
    /// </summary>
    public TimeSpan ExecuteTimeout { get; set; } = TimeSpan.FromMinutes(2); // Default timeout
}
