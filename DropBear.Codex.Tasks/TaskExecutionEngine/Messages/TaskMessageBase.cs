#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Base class for all task-related messages providing common functionality.
///     Implements <see cref="IResettable" /> for safe reuse from object pooling.
/// </summary>
public abstract class TaskMessageBase : IResettable
{
    protected TaskMessageBase()
    {
        TaskName = string.Empty;
    }

    /// <summary>
    ///     The name of the task that this message refers to.
    /// </summary>
    public string TaskName { get; protected set; }

    /// <summary>
    ///     Resets the message to an initial state for reuse by object pooling.
    /// </summary>
    public virtual void Reset()
    {
        TaskName = string.Empty;
    }

    /// <summary>
    ///     Validates that the provided task name is neither null nor whitespace.
    /// </summary>
    /// <param name="taskName">The task name to validate.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="taskName" /> is invalid.</exception>
    protected void ValidateTaskName(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }
    }
}
