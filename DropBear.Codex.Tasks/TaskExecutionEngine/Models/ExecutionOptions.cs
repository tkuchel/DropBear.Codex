namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Represents the configuration options for task execution within the execution engine.
/// </summary>
public sealed class ExecutionOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether tasks should be executed in parallel.
    /// </summary>
    public bool EnableParallelExecution { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether the execution engine should stop executing tasks upon the first failure.
    /// </summary>
    public bool StopOnFirstFailure { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether verbose logging is enabled.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;

    /// <summary>
    ///     Validates the execution options to ensure they are in a consistent state.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the configuration is invalid.
    /// </exception>
    public void Validate()
    {
        // Add validation logic if necessary.
        // Currently, there are no invalid combinations, but this method provides a placeholder
        // for future validation needs.

        // Example validation (if needed in the future):
        // if (EnableParallelExecution && SomeOtherOption == false)
        // {
        //     throw new InvalidOperationException("Parallel execution requires SomeOtherOption to be true.");
        // }
    }
}
