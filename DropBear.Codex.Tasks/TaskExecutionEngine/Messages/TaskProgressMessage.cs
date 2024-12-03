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
    /// <param name="taskProgressPercentage">The progress percentage of the individual task.</param>
    /// <param name="overallCompletedTasks">The number of overall tasks completed so far.</param>
    /// <param name="overallTotalTasks">The total number of overall tasks to be executed.</param>
    /// <param name="status">The status of the task.</param>
    /// <param name="message">An optional message providing additional information.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="taskName" /> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="overallCompletedTasks" /> or <paramref name="overallTotalTasks" /> are negative,
    ///     or when <paramref name="overallCompletedTasks" /> is greater than <paramref name="overallTotalTasks" />.
    /// </exception>
    public TaskProgressMessage(
        string taskName,
        double? taskProgressPercentage,
        int? overallCompletedTasks,
        int? overallTotalTasks,
        TaskStatus status,
        string message = "")
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name cannot be null or whitespace.", nameof(taskName));
        }

        TaskName = taskName;
        TaskProgressPercentage = taskProgressPercentage;
        Status = status;
        Message = message ?? string.Empty;
        OverallCompletedTasks = overallCompletedTasks;
        OverallTotalTasks = overallTotalTasks;

        if (OverallCompletedTasks.HasValue && OverallTotalTasks.HasValue)
        {
            if (OverallCompletedTasks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(overallCompletedTasks),
                    "Completed tasks cannot be negative.");
            }

            if (OverallTotalTasks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(overallTotalTasks), "Total tasks cannot be negative.");
            }

            if (OverallCompletedTasks > OverallTotalTasks)
            {
                throw new ArgumentOutOfRangeException(nameof(overallCompletedTasks),
                    "Completed tasks cannot exceed total tasks.");
            }
        }
    }

    /// <summary>
    ///     Gets the name of the task.
    /// </summary>
    public string TaskName { get; }

    /// <summary>
    ///     Gets the progress percentage of the individual task, if applicable.
    /// </summary>
    public double? TaskProgressPercentage { get; }

    /// <summary>
    ///     Gets the status of the task.
    /// </summary>
    public TaskStatus Status { get; }

    /// <summary>
    ///     Gets the number of overall tasks completed so far, if applicable.
    /// </summary>
    public int? OverallCompletedTasks { get; }

    /// <summary>
    ///     Gets the total number of overall tasks to be executed, if applicable.
    /// </summary>
    public int? OverallTotalTasks { get; }

    /// <summary>
    ///     Gets the overall progress percentage, if applicable.
    /// </summary>
    public double? OverallProgressPercentage =>
        OverallTotalTasks is > 0
            ? (double)OverallCompletedTasks! / OverallTotalTasks * 100
            : null;

    /// <summary>
    ///     Gets an optional message providing additional information.
    /// </summary>
    public string Message { get; }
}
