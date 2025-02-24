#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Models;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Represents a message indicating that a task has started execution.
///     Supports object pooling for memory efficiency.
/// </summary>
public sealed class TaskStartedMessage : TaskMessageBase
{
    public void Initialize(string taskName)
    {
        ValidateTaskName(taskName);
        TaskName = taskName;
    }

    public static TaskStartedMessage Get(string taskName)
    {
        var message = ObjectPools<TaskStartedMessage>.Rent();
        message.Initialize(taskName);
        return message;
    }

    public static void Return(TaskStartedMessage message)
    {
        ObjectPools<TaskStartedMessage>.Return(message);
    }
}
