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

    /// <summary>
    ///     Creates a validation error for when a task name is invalid.
    /// </summary>
    /// <param name="taskName">The invalid task name.</param>
    public static TaskValidationError InvalidName(string taskName)
    {
        return new TaskValidationError("Task name cannot be null or empty", taskName);
    }

    /// <summary>
    ///     Creates a validation error for when a task property has an invalid value.
    /// </summary>
    /// <param name="taskName">The name of the task.</param>
    /// <param name="propertyName">The name of the invalid property.</param>
    /// <param name="reason">The reason why the property is invalid.</param>
    public static TaskValidationError InvalidProperty(string taskName, string propertyName, string reason)
    {
        return new TaskValidationError($"Property '{propertyName}' is invalid: {reason}", taskName);
    }
}
