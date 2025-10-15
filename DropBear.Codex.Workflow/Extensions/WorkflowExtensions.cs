#region

using System.Text;
using DropBear.Codex.Workflow.Builder;
using DropBear.Codex.Workflow.Metrics;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Extensions;

/// <summary>
///     Extension methods for workflow results and components.
/// </summary>
public static class WorkflowExtensions
{
    /// <summary>
    ///     Creates a new workflow builder.
    /// </summary>
    public static WorkflowBuilder<TContext> CreateWorkflow<TContext>(
        string workflowId,
        string displayName,
        Version? version = null) where TContext : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new WorkflowBuilder<TContext>(workflowId, displayName, version);
    }

    /// <summary>
    ///     Checks if a workflow result contains execution traces.
    /// </summary>
    public static bool HasExecutionTrace<TContext>(this WorkflowResult<TContext> result) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.ExecutionTrace is not null && result.ExecutionTrace.Count > 0;
    }

    /// <summary>
    ///     Gets the total execution time from workflow metrics.
    /// </summary>
    public static TimeSpan GetExecutionTime<TContext>(this WorkflowResult<TContext> result) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Metrics?.TotalExecutionTime ?? TimeSpan.Zero;
    }

    /// <summary>
    ///     Gets failed steps from the execution trace.
    /// </summary>
    public static IEnumerable<StepExecutionTrace> GetFailedSteps<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExecutionTrace is null)
        {
            return Enumerable.Empty<StepExecutionTrace>();
        }

        return result.ExecutionTrace.Where(trace => !trace.Result.IsSuccess);
    }

    /// <summary>
    ///     Gets successful steps from the execution trace.
    /// </summary>
    public static IEnumerable<StepExecutionTrace> GetSuccessfulSteps<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExecutionTrace is null)
        {
            return Enumerable.Empty<StepExecutionTrace>();
        }

        return result.ExecutionTrace.Where(trace => trace.Result.IsSuccess);
    }

    /// <summary>
    ///     Gets steps that were retried from the execution trace.
    /// </summary>
    public static IEnumerable<StepExecutionTrace> GetRetriedSteps<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExecutionTrace is null)
        {
            return Enumerable.Empty<StepExecutionTrace>();
        }

        return result.ExecutionTrace.Where(trace => trace.RetryAttempts > 0);
    }

    /// <summary>
    ///     Gets the longest running step from the execution trace.
    /// </summary>
    public static StepExecutionTrace? GetLongestRunningStep<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExecutionTrace is null || result.ExecutionTrace.Count == 0)
        {
            return null;
        }

        StepExecutionTrace? longest = null;
        TimeSpan longestDuration = TimeSpan.Zero;

        foreach (StepExecutionTrace trace in result.ExecutionTrace)
        {
            TimeSpan duration = trace.ExecutionTime;
            if (duration > longestDuration)
            {
                longestDuration = duration;
                longest = trace;
            }
        }

        return longest;
    }

    /// <summary>
    ///     Gets the shortest running step from the execution trace.
    /// </summary>
    public static StepExecutionTrace? GetShortestRunningStep<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExecutionTrace is null || result.ExecutionTrace.Count == 0)
        {
            return null;
        }

        StepExecutionTrace? shortest = null;
        TimeSpan shortestDuration = TimeSpan.MaxValue;

        foreach (StepExecutionTrace trace in result.ExecutionTrace)
        {
            TimeSpan duration = trace.ExecutionTime;
            if (duration < shortestDuration)
            {
                shortestDuration = duration;
                shortest = trace;
            }
        }

        return shortest;
    }

    /// <summary>
    ///     Calculates the average step execution time.
    /// </summary>
    public static TimeSpan GetAverageStepExecutionTime<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExecutionTrace is null || result.ExecutionTrace.Count == 0)
        {
            return TimeSpan.Zero;
        }

        long totalTicks = 0;
        foreach (StepExecutionTrace trace in result.ExecutionTrace)
        {
            totalTicks += trace.ExecutionTime.Ticks;
        }

        long averageTicks = totalTicks / result.ExecutionTrace.Count;
        return new TimeSpan(averageTicks);
    }

    /// <summary>
    ///     Creates a summary of workflow execution performance.
    /// </summary>
    public static Dictionary<string, object> GetPerformanceSummary<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        var summary = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["WorkflowSuccess"] = result.IsSuccess,
            ["TotalExecutionTime"] = result.GetExecutionTime(),
            ["StepsExecuted"] = result.Metrics?.StepsExecuted ?? 0,
            ["StepsFailed"] = result.Metrics?.StepsFailed ?? 0,
            ["TotalRetries"] = result.Metrics?.TotalRetries ?? 0
        };

        if (result.ExecutionTrace is not null && result.ExecutionTrace.Count > 0)
        {
            long totalTicks = 0;
            TimeSpan longestDuration = TimeSpan.Zero;
            TimeSpan shortestDuration = TimeSpan.MaxValue;
            int retriedCount = 0;

            foreach (StepExecutionTrace trace in result.ExecutionTrace)
            {
                TimeSpan duration = trace.ExecutionTime;
                totalTicks += duration.Ticks;

                if (duration > longestDuration)
                {
                    longestDuration = duration;
                }

                if (duration < shortestDuration)
                {
                    shortestDuration = duration;
                }

                if (trace.RetryAttempts > 0)
                {
                    retriedCount++;
                }
            }

            long averageTicks = totalTicks / result.ExecutionTrace.Count;
            summary["AverageStepTime"] = new TimeSpan(averageTicks);
            summary["LongestStepTime"] = longestDuration;
            summary["ShortestStepTime"] = shortestDuration == TimeSpan.MaxValue ? TimeSpan.Zero : shortestDuration;
            summary["StepsWithRetries"] = retriedCount;
        }

        if (result.Metrics?.PeakMemoryUsage.HasValue == true)
        {
            summary["PeakMemoryUsageMB"] = result.Metrics.PeakMemoryUsage.Value / (1024.0 * 1024.0);
        }

        return summary;
    }

    /// <summary>
    ///     Gets the total number of retries across all steps.
    /// </summary>
    public static int GetTotalRetries<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Metrics?.TotalRetries ?? 0;
    }

    /// <summary>
    ///     Gets the success rate as a percentage (0-100).
    /// </summary>
    public static double GetSuccessRate<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Metrics?.SuccessRate ?? 0;
    }

    /// <summary>
    ///     Gets the failure rate as a percentage (0-100).
    /// </summary>
    public static double GetFailureRate<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Metrics?.FailureRate ?? 0;
    }

    /// <summary>
    ///     Gets the number of steps that were compensated.
    /// </summary>
    public static int GetCompensatedStepsCount<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExecutionTrace is null)
        {
            return 0;
        }

        return result.ExecutionTrace.Count(trace =>
            trace.Metadata?.ContainsKey("Compensated") == true &&
            trace.Metadata["Compensated"] is bool compensated &&
            compensated);
    }

    /// <summary>
    ///     Checks if the workflow was suspended.
    /// </summary>
    public static bool IsSuspended<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuspended;
    }

    /// <summary>
    ///     Gets the signal name that the workflow is waiting for (if suspended).
    /// </summary>
    public static string? GetWaitingSignal<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.SuspendedSignalName;
    }

    /// <summary>
    ///     Creates a detailed execution report as a formatted string.
    /// </summary>
    public static string ToExecutionReport<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        var report = new StringBuilder();
        report.AppendLine("=== Workflow Execution Report ===");
        _ = report.AppendLine($"Status: {(result.IsSuccess ? "Success" : "Failed")}");
        _ = report.AppendLine($"Total Execution Time: {result.GetExecutionTime()}");

        if (result.Metrics != null)
        {
            _ = report.AppendLine($"Steps Executed: {result.Metrics.StepsExecuted}");
            _ = report.AppendLine($"Steps Succeeded: {result.Metrics.StepsSucceeded}");
            _ = report.AppendLine($"Steps Failed: {result.Metrics.StepsFailed}");
            _ = report.AppendLine($"Total Retries: {result.Metrics.TotalRetries}");
            _ = report.AppendLine($"Success Rate: {result.Metrics.SuccessRate:F2}%");
        }

        if (result.IsSuspended)
        {
            _ = report.AppendLine($"Workflow Suspended - Waiting for signal: {result.SuspendedSignalName}");
        }

        if (!result.IsSuccess && result.ErrorMessage != null)
        {
            _ = report.AppendLine($"Error: {result.ErrorMessage}");
        }

        if (result.HasExecutionTrace())
        {
            report.AppendLine("\n=== Step Execution Details ===");
            foreach (StepExecutionTrace trace in result.ExecutionTrace!)
            {
                report.AppendLine(trace.ToSummaryString());
            }
        }

        return report.ToString();
    }
}
