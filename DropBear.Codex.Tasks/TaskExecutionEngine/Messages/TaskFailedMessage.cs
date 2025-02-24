#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Models;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Represents a message indicating that a task has failed.
///     Supports object pooling for memory efficiency.
/// </summary>
public sealed class TaskFailedMessage : TaskMessageBase
{
    /// <summary>
    ///     Gets the exception that caused the task to fail.
    /// </summary>
    public Exception Exception { get; private set; } = null!;

    /// <summary>
    ///     Initializes this message instance with a specific task name and exception.
    /// </summary>
    public void Initialize(string taskName, Exception exception)
    {
        ValidateTaskName(taskName);
        ArgumentNullException.ThrowIfNull(exception);

        TaskName = taskName;
        Exception = exception;
    }

    /// <summary>
    ///     Retrieves a pooled <see cref="TaskFailedMessage" /> and initializes it.
    /// </summary>
    public static TaskFailedMessage Get(string taskName, Exception exception)
    {
        var message = ObjectPools<TaskFailedMessage>.Rent();
        message.Initialize(taskName, exception);
        return message;
    }

    /// <summary>
    ///     Returns this message instance to the object pool.
    /// </summary>
    public static void Return(TaskFailedMessage message)
    {
        ObjectPools<TaskFailedMessage>.Return(message);
    }

    /// <summary>
    ///     Resets this instance for reuse, clearing the <see cref="Exception" />.
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        Exception = null!;
    }
}
