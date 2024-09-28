namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Represents a message indicating that a task has started execution.
/// </summary>
public sealed class TaskStartedMessage
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TaskStartedMessage" /> class.
    /// </summary>
    /// <param name="taskName">The name of the task that has started.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="taskName" /> is null or whitespace.</exception>
    public TaskStartedMessage(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        TaskName = taskName;
    }

    /// <summary>
    ///     Gets the name of the task that has started.
    /// </summary>
    public string TaskName { get; }
}
