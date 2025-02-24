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

    // Cache these calculations to avoid recomputing
    private double? _overallProgressPercentage;

    public double? TaskProgressPercentage { get; private set; }
    public TaskStatus Status { get; private set; }
    public int? OverallCompletedTasks { get; private set; }
    public int? OverallTotalTasks { get; private set; }
    public string Message { get; private set; } = string.Empty;

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

        // Reset cached calculation
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

    public static TaskProgressMessage Get(
        string taskName,
        double? taskProgressPercentage,
        int? overallCompletedTasks,
        int? overallTotalTasks,
        TaskStatus status,
        string? message = "")
    {
        var msg = ObjectPools<TaskProgressMessage>.Rent();
        msg.Initialize(taskName, taskProgressPercentage, overallCompletedTasks,
            overallTotalTasks, status, message);
        return msg;
    }

    public static void Return(TaskProgressMessage message)
    {
        ObjectPools<TaskProgressMessage>.Return(message);
    }
}
