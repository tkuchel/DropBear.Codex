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
}
