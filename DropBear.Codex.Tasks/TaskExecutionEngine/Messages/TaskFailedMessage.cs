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
    public Exception Exception { get; private set; } = null!;

    public void Initialize(string taskName, Exception exception)
    {
        ValidateTaskName(taskName);
        ArgumentNullException.ThrowIfNull(exception);

        TaskName = taskName;
        Exception = exception;
    }

    public static TaskFailedMessage Get(string taskName, Exception exception)
    {
        var message = ObjectPools<TaskFailedMessage>.Rent();
        message.Initialize(taskName, exception);
        return message;
    }

    public static void Return(TaskFailedMessage message)
    {
        ObjectPools<TaskFailedMessage>.Return(message);
    }

    public override void Reset()
    {
        base.Reset();
        Exception = null!;
    }
}
