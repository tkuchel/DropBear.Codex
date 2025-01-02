#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Messages;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Models;

/// <summary>
///     Manages object pools for various message types used in task execution.
/// </summary>
internal static class MessagePools
{
    /// <summary>
    ///     Pool for progress messages used during task execution.
    /// </summary>
    public static readonly ObjectPool<TaskProgressMessage> ProgressMessagePool =
        new DefaultObjectPoolProvider().Create(new DefaultPooledObjectPolicy<TaskProgressMessage>());

    /// <summary>
    ///     Pool for task started messages.
    /// </summary>
    public static readonly ObjectPool<TaskStartedMessage> StartedMessagePool =
        new DefaultObjectPoolProvider().Create(new DefaultPooledObjectPolicy<TaskStartedMessage>());

    /// <summary>
    ///     Pool for task completed messages.
    /// </summary>
    public static readonly ObjectPool<TaskCompletedMessage> CompletedMessagePool =
        new DefaultObjectPoolProvider().Create(new DefaultPooledObjectPolicy<TaskCompletedMessage>());

    /// <summary>
    ///     Pool for task failed messages.
    /// </summary>
    public static readonly ObjectPool<TaskFailedMessage> FailedMessagePool =
        new DefaultObjectPoolProvider().Create(new DefaultPooledObjectPolicy<TaskFailedMessage>());
}
