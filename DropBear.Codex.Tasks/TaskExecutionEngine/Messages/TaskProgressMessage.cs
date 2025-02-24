#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using TaskStatus = DropBear.Codex.Tasks.TaskExecutionEngine.Enums.TaskStatus;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Messages;

/// <summary>
///     Represents a message that provides progress information about a task execution.
/// </summary>
public sealed class TaskProgressMessage : TaskMessageBase
{
    private bool _overallProgressCalculated;
    private double? _overallProgressPercentage;

    /// <summary>
    ///     Indicates the percentage of completion for the specific task.
    /// </summary>
    public double? TaskProgressPercentage { get; private set; }

    /// <summary>
    ///     Indicates the current status of the task (e.g., InProgress, Completed).
    /// </summary>
    public TaskStatus Status { get; private set; }

    /// <summary>
    ///     Number of tasks completed overall, if relevant.
    /// </summary>
    public int? OverallCompletedTasks { get; private set; }

    /// <summary>
    ///     Total number of tasks overall, if relevant.
    /// </summary>
    public int? OverallTotalTasks { get; private set; }

    /// <summary>
    ///     An optional message describing the current state of the task.
    /// </summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    ///     The aggregated progress across all tasks, computed lazily.
    /// </summary>
    public double? OverallProgressPercentage
    {
        get
        {
            if (!_overallProgressCalculated)
            {
                _overallProgressPercentage = CalculateOverallProgress();
                _overallProgressCalculated = true;
            }

            return _overallProgressPercentage;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double? CalculateOverallProgress()
    {
        if (!OverallTotalTasks.HasValue || !OverallCompletedTasks.HasValue || OverallTotalTasks.Value <= 0)
        {
            return null;
        }

        return (double)OverallCompletedTasks.Value / OverallTotalTasks.Value * 100;
    }

    /// <summary>
    ///     Initializes this message instance with progress details.
    /// </summary>
    public void Initialize(
        string taskName,
        double? taskProgressPercentage,
        int? overallCompletedTasks,
        int? overallTotalTasks,
        TaskStatus status,
        string? message = "")
    {
        ValidateTaskName(taskName);
        ValidateProgress(taskProgressPercentage, overallCompletedTasks, overallTotalTasks);

        TaskName = taskName;
        TaskProgressPercentage = taskProgressPercentage;
        OverallCompletedTasks = overallCompletedTasks;
        OverallTotalTasks = overallTotalTasks;
        Status = status;
        Message = message ?? string.Empty;

        _overallProgressCalculated = false;
        _overallProgressPercentage = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateProgress(
        double? taskProgress,
        int? completedTasks,
        int? totalTasks)
    {
        if (taskProgress is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(taskProgress),
                "Task progress must be between 0 and 100.");
        }

        if (completedTasks.HasValue)
        {
            if (completedTasks.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(completedTasks),
                    "Completed tasks cannot be negative.");
            }

            if (totalTasks.HasValue)
            {
                if (totalTasks.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(totalTasks),
                        "Total tasks cannot be negative.");
                }

                if (completedTasks.Value > totalTasks.Value)
                {
                    throw new ArgumentOutOfRangeException(nameof(completedTasks),
                        "Completed tasks cannot exceed total tasks.");
                }
            }
        }
    }

    /// <summary>
    ///     Resets this instance for reuse, clearing all progress-related fields.
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        TaskProgressPercentage = null;
        OverallCompletedTasks = null;
        OverallTotalTasks = null;
        Status = TaskStatus.NotStarted;
        Message = string.Empty;
        _overallProgressCalculated = false;
        _overallProgressPercentage = null;
    }

    /// <summary>
    ///     Retrieves a pooled <see cref="TaskProgressMessage" /> and initializes it.
    /// </summary>
    public static TaskProgressMessage Get(
        string taskName,
        double? taskProgressPercentage,
        int? overallCompletedTasks,
        int? overallTotalTasks,
        TaskStatus status,
        string? message = "")
    {
        var msg = ObjectPools<TaskProgressMessage>.Rent();
        msg.Initialize(taskName, taskProgressPercentage, overallCompletedTasks, overallTotalTasks, status, message);
        return msg;
    }

    /// <summary>
    ///     Returns this message instance to the object pool.
    /// </summary>
    public static void Return(TaskProgressMessage message)
    {
        ObjectPools<TaskProgressMessage>.Return(message);
    }
}
