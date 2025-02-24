namespace DropBear.Codex.Tasks.Errors;

/// <summary>
///     Represents an error that occurs specifically during task validation.
/// </summary>
public sealed record TaskValidationError : TaskExecutionError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="TaskValidationError" />.
    /// </summary>
    /// <param name="message">A descriptive error message (will be prefixed with "Task Validation Error: ").</param>
    /// <param name="taskName">The name of the task that failed validation.</param>
    /// <param name="exception">The underlying exception, if any.</param>
    public TaskValidationError(string message, string taskName, Exception? exception = null)
        : base($"Task Validation Error: {message}", taskName, exception)
    {
    }
}
