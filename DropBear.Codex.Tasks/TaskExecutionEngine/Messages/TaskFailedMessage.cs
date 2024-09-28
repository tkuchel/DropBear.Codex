namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Represents a message indicating that a task has failed.
/// </summary>
public sealed class TaskFailedMessage
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TaskFailedMessage" /> class.
    /// </summary>
    /// <param name="taskName">The name of the failed task.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="taskName" /> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception" /> is null.</exception>
    public TaskFailedMessage(string taskName, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        TaskName = taskName;
    }

    /// <summary>
    ///     Gets the name of the failed task.
    /// </summary>
    public string TaskName { get; }

    /// <summary>
    ///     Gets the exception that caused the task to fail.
    /// </summary>
    public Exception Exception { get; }
}
