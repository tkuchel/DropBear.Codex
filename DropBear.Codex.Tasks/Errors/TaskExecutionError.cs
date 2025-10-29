#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Tasks.Errors;

/// <summary>
///     Represents an error that occurs during task execution.
/// </summary>
public record TaskExecutionError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of <see cref="TaskExecutionError" />.
    /// </summary>
    /// <param name="message">A descriptive error message.</param>
    /// <param name="taskName">The name of the task that failed (optional).</param>
    /// <param name="exception">The underlying exception, if any.</param>
    public TaskExecutionError(string message, string? taskName = null, Exception? exception = null)
        : base(message)
    {
        TaskName = taskName;
        Exception = exception;
    }

    /// <summary>
    ///     The name of the task that caused this error, if relevant.
    /// </summary>
    public string? TaskName { get; }

    /// <summary>
    ///     The underlying exception that triggered this error, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    ///     Creates a task execution error for when a task times out.
    /// </summary>
    /// <param name="taskName">The name of the task that timed out.</param>
    /// <param name="timeout">The timeout duration that was exceeded.</param>
    public static TaskExecutionError Timeout(string taskName, TimeSpan timeout)
    {
        return new TaskExecutionError(
            $"Task '{taskName}' execution timed out after {timeout.TotalSeconds} seconds",
            taskName);
    }

    /// <summary>
    ///     Creates a task execution error for when a task fails with an exception.
    /// </summary>
    /// <param name="taskName">The name of the task that failed.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    public static TaskExecutionError Failed(string taskName, Exception exception)
    {
        return new TaskExecutionError(
            $"Task '{taskName}' failed with exception: {exception.Message}",
            taskName,
            exception);
    }

    /// <summary>
    ///     Creates a task execution error for when a task is cancelled.
    /// </summary>
    /// <param name="taskName">The name of the task that was cancelled.</param>
    public static TaskExecutionError Cancelled(string taskName)
    {
        return new TaskExecutionError(
            $"Task '{taskName}' was cancelled",
            taskName);
    }
}
