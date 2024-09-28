namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Represents a message indicating that a task has completed successfully.
/// </summary>
public sealed class TaskCompletedMessage
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TaskCompletedMessage" /> class.
    /// </summary>
    /// <param name="taskName">The name of the completed task.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="taskName" /> is null or whitespace.</exception>
    public TaskCompletedMessage(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        TaskName = taskName;
    }

    /// <summary>
    ///     Gets the name of the completed task.
    /// </summary>
    public string TaskName { get; }
}
