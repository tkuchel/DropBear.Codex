#region

using TaskStatus = DropBear.Codex.Tasks.TaskExecutionEngine.Enums.TaskStatus;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Represents a message that provides progress information about a task execution.
/// </summary>
public sealed class TaskProgressMessage
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TaskProgressMessage" /> class.
    /// </summary>
    /// <param name="taskName">The name of the task.</param>
    /// <param name="completedTasks">The number of tasks completed so far.</param>
    /// <param name="totalTasks">The total number of tasks to be executed.</param>
    /// <param name="status">The status of the task.</param>
    /// <param name="message">An optional message providing additional information.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="taskName" /> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="completedTasks" /> or <paramref name="totalTasks" /> are negative,
    ///     or when <paramref name="completedTasks" /> is greater than <paramref name="totalTasks" />.
    /// </exception>
    public TaskProgressMessage(
        string taskName,
        int completedTasks,
        int totalTasks,
        TaskStatus status,
        string message = "")
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        if (completedTasks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completedTasks), "Completed tasks cannot be negative.");
        }

        if (totalTasks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTasks), "Total tasks cannot be negative.");
        }

        if (completedTasks > totalTasks)
        {
            throw new ArgumentOutOfRangeException(nameof(completedTasks), "Completed tasks cannot exceed total tasks.");
        }

        TaskName = taskName;
        CompletedTasks = completedTasks;
        TotalTasks = totalTasks;
        Status = status;
        Message = message ?? string.Empty;
    }

    /// <summary>
    ///     Gets the name of the task.
    /// </summary>
    public string TaskName { get; }

    /// <summary>
    ///     Gets the number of tasks completed so far.
    /// </summary>
    public int CompletedTasks { get; }

    /// <summary>
    ///     Gets the total number of tasks to be executed.
    /// </summary>
    public int TotalTasks { get; }

    /// <summary>
    ///     Gets the status of the task.
    /// </summary>
    public TaskStatus Status { get; }

    /// <summary>
    ///     Gets the overall progress percentage.
    /// </summary>
    public double OverallProgress => TotalTasks > 0 ? (double)CompletedTasks / TotalTasks * 100 : 0;

    /// <summary>
    ///     Gets an optional message providing additional information.
    /// </summary>
    public string Message { get; }
}
