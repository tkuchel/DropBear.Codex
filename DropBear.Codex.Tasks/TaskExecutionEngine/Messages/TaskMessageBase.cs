#region

using DropBear.Codex.Tasks.TaskExecutionEngine.Interfaces;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Base class for all task-related messages providing common functionality
/// </summary>
public abstract class TaskMessageBase : IResettable
{
    protected TaskMessageBase()
    {
        TaskName = string.Empty;
    }

    public string TaskName { get; protected set; }

    public virtual void Reset()
    {
        TaskName = string.Empty;
    }

    protected void ValidateTaskName(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }
    }
}
