#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Models;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Represents a message indicating that a task has completed successfully.
///     Supports object pooling for memory efficiency.
/// </summary>
public sealed class TaskCompletedMessage : TaskMessageBase
{
    /// <summary>
    ///     Initializes this message instance with a specific task name.
    /// </summary>
    public void Initialize(string taskName)
    {
        ValidateTaskName(taskName);
        TaskName = taskName;
    }

    /// <summary>
    ///     Retrieves a pooled <see cref="TaskCompletedMessage" /> and initializes it.
    /// </summary>
    public static TaskCompletedMessage Get(string taskName)
    {
        var message = ObjectPools<TaskCompletedMessage>.Rent();
        message.Initialize(taskName);
        return message;
    }

    /// <summary>
    ///     Returns this message instance to the object pool.
    /// </summary>
    public static void Return(TaskCompletedMessage message)
    {
        ObjectPools<TaskCompletedMessage>.Return(message);
    }
}
