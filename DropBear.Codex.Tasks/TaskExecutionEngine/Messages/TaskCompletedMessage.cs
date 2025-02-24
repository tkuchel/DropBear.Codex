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
    public void Initialize(string taskName)
    {
        ValidateTaskName(taskName);
        TaskName = taskName;
    }

    public static TaskCompletedMessage Get(string taskName)
    {
        var message = ObjectPools<TaskCompletedMessage>.Rent();
        message.Initialize(taskName);
        return message;
    }

    public static void Return(TaskCompletedMessage message)
    {
        ObjectPools<TaskCompletedMessage>.Return(message);
    }
}
