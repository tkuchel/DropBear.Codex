#region

using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Represents a message indicating that a task has failed.
///     Supports object pooling for memory efficiency.
/// </summary>
public sealed class TaskFailedMessage
{
    private static readonly ObjectPool<TaskFailedMessage> Pool =
        new DefaultObjectPoolProvider().Create(new DefaultPooledObjectPolicy<TaskFailedMessage>());

    /// <summary>
    ///     Initializes a new instance of the <see cref="TaskFailedMessage" /> class.
    /// </summary>
    public TaskFailedMessage()
    {
        TaskName = string.Empty; // Default for pooling
        Exception = null!;
    }

    /// <summary>
    ///     Gets the name of the failed task.
    /// </summary>
    public string TaskName { get; private set; }

    /// <summary>
    ///     Gets the exception that caused the task to fail.
    /// </summary>
    public Exception Exception { get; private set; }

    /// <summary>
    ///     Initializes the message with the task name and exception.
    /// </summary>
    /// <param name="taskName">The name of the failed task.</param>
    /// <param name="exception">The exception causing the failure.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="taskName" /> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception" /> is null.</exception>
    public void Initialize(string taskName, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        TaskName = taskName;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    /// <summary>
    ///     Retrieves an instance of <see cref="TaskFailedMessage" /> from the pool.
    /// </summary>
    public static TaskFailedMessage Get(string taskName, Exception exception)
    {
        var message = Pool.Get();
        message.Initialize(taskName, exception);
        return message;
    }

    /// <summary>
    ///     Returns an instance of <see cref="TaskFailedMessage" /> to the pool.
    /// </summary>
    public static void Return(TaskFailedMessage message)
    {
        message.TaskName = string.Empty; // Reset state
        message.Exception = null!; // Nullify reference
        Pool.Return(message);
    }
}
