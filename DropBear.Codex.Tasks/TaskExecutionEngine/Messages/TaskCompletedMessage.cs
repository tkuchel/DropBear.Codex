#region

using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Represents a message indicating that a task has completed successfully.
///     Supports object pooling for memory efficiency.
/// </summary>
public sealed class TaskCompletedMessage
{
    private static readonly ObjectPool<TaskCompletedMessage> Pool =
        new DefaultObjectPoolProvider().Create(new DefaultPooledObjectPolicy<TaskCompletedMessage>());

    /// <summary>
    ///     Initializes a new instance of the <see cref="TaskCompletedMessage" /> class.
    /// </summary>
    public TaskCompletedMessage()
    {
        TaskName = string.Empty; // Default for pooling
    }

    /// <summary>
    ///     Gets the name of the completed task.
    /// </summary>
    public string TaskName { get; private set; }

    /// <summary>
    ///     Initializes the message with the task name.
    /// </summary>
    /// <param name="taskName">The name of the completed task.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="taskName" /> is null or whitespace.</exception>
    public void Initialize(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        TaskName = taskName;
    }

    /// <summary>
    ///     Retrieves an instance of <see cref="TaskCompletedMessage" /> from the pool.
    /// </summary>
    public static TaskCompletedMessage Get(string taskName)
    {
        var message = Pool.Get();
        message.Initialize(taskName);
        return message;
    }

    /// <summary>
    ///     Returns an instance of <see cref="TaskCompletedMessage" /> to the pool.
    /// </summary>
    public static void Return(TaskCompletedMessage message)
    {
        message.TaskName = string.Empty; // Reset state
        Pool.Return(message);
    }
}
