#region

using DropBear.Codex.Workflow.Builder;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Metrics;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Extensions;

/// <summary>
///     Extension methods for working with workflow results and operations.
///     Optimized for performance with minimal LINQ overhead.
/// </summary>
public static class WorkflowExtensions
{
    /// <summary>
    ///     Executes a workflow with a factory-created context.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="engine">The workflow engine</param>
    /// <param name="definition">The workflow definition</param>
    /// <param name="contextFactory">Factory function to create the initial context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The workflow execution result</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
    public static ValueTask<WorkflowResult<TContext>> ExecuteAsync<TContext>(
        this IWorkflowEngine engine,
        IWorkflowDefinition<TContext> definition,
        Func<TContext> contextFactory,
        CancellationToken cancellationToken = default) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(contextFactory);

        TContext context = contextFactory();
        return engine.ExecuteAsync(definition, context, cancellationToken);
    }

    /// <summary>
    ///     Executes a workflow and returns only the success status.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="engine">The workflow engine</param>
    /// <param name="definition">The workflow definition</param>
    /// <param name="context">The workflow context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the workflow succeeded, false otherwise</returns>
    public static async ValueTask<bool> TryExecuteAsync<TContext>(
        this IWorkflowEngine engine,
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class
    {
        WorkflowResult<TContext> result =
            await engine.ExecuteAsync(definition, context, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess;
    }

    /// <summary>
    ///     Creates a workflow builder with the specified ID and display name.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="workflowId">Unique workflow identifier</param>
    /// <param name="displayName">Human-readable workflow name</param>
    /// <param name="version">Workflow version</param>
    /// <returns>A new workflow builder instance</returns>
    public static WorkflowBuilder<TContext> CreateWorkflow<TContext>(
        string workflowId,
        string displayName,
        Version? version = null) where TContext : class =>
        new(workflowId, displayName, version);

    /// <summary>
    ///     Adds metadata to a step result (creates a new instance).
    /// </summary>
    /// <param name="result">The step result</param>
    /// <param name="key">Metadata key</param>
    /// <param name="value">Metadata value</param>
    /// <returns>A new step result with the added metadata</returns>
    /// <exception cref="ArgumentNullException">Thrown when result, key, or value is null</exception>
    public static StepResult WithMetadata(this StepResult result, string key, object value)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        Dictionary<string, object> metadata = result.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                                              ?? new Dictionary<string, object>();
        metadata[key] = value;

        return result with { Metadata = metadata };
    }

    /// <summary>
    ///     Checks if a workflow result contains execution traces.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>True if execution traces are available</returns>
    public static bool HasExecutionTrace<TContext>(this WorkflowResult<TContext> result) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.ExecutionTrace is not null && result.ExecutionTrace.Count > 0;
    }

    /// <summary>
    ///     Gets the total execution time from workflow metrics.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>The total execution time, or TimeSpan.Zero if metrics are not available</returns>
    public static TimeSpan GetExecutionTime<TContext>(this WorkflowResult<TContext> result) where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Metrics?.TotalExecutionTime ?? TimeSpan.Zero;
    }

    /// <summary>
    ///     Gets failed steps from the execution trace.
    ///     OPTIMIZED: Returns early if no trace available.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>Collection of failed step execution traces</returns>
    public static IEnumerable<StepExecutionTrace> GetFailedSteps<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExecutionTrace is null)
        {
            return [];
        }

        return result.ExecutionTrace.Where(trace => !trace.Result.IsSuccess);
    }

    /// <summary>
    ///     Gets successful steps from the execution trace.
    ///     OPTIMIZED: Returns early if no trace available.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>Collection of successful step execution traces</returns>
    public static IEnumerable<StepExecutionTrace> GetSuccessfulSteps<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExecutionTrace is null)
        {
            return [];
        }

        return result.ExecutionTrace.Where(trace => trace.Result.IsSuccess);
    }

    /// <summary>
    ///     Gets steps that were retried from the execution trace.
    ///     OPTIMIZED: Returns early if no trace available.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>Collection of step execution traces for steps that were retried</returns>
    public static IEnumerable<StepExecutionTrace> GetRetriedSteps<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExecutionTrace is null)
        {
            return [];
        }

        return result.ExecutionTrace.Where(trace => trace.RetryAttempts > 0);
    }

    /// <summary>
    ///     Gets the longest running step from the execution trace.
    ///     OPTIMIZED: Single-pass algorithm.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>The step execution trace with the longest duration, or null if no traces available</returns>
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
    ///     OPTIMIZED: Single-pass algorithm.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>The step execution trace with the shortest duration, or null if no traces available</returns>
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
    ///     OPTIMIZED: Single-pass algorithm.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>The average step execution time, or TimeSpan.Zero if no traces available</returns>
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
    ///     OPTIMIZED: Single-pass algorithm to gather all statistics.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>A dictionary containing performance summary metrics</returns>
    public static IDictionary<string, object> GetPerformanceSummary<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);

        var summary = new Dictionary<string, object>
        {
            ["WorkflowSuccess"] = result.IsSuccess,
            ["TotalExecutionTime"] = result.GetExecutionTime(),
            ["StepsExecuted"] = result.Metrics?.StepsExecuted ?? 0,
            ["StepsFailed"] = result.Metrics?.StepsFailed ?? 0,
            ["TotalRetries"] = result.Metrics?.TotalRetries ?? 0
        };

        // OPTIMIZED: Single pass through traces to collect all statistics
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
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>The total number of retry attempts</returns>
    public static int GetTotalRetries<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Metrics?.TotalRetries ?? 0;
    }

    /// <summary>
    ///     Gets the success rate as a percentage (0-100).
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>Success rate percentage, or 0 if no steps were executed</returns>
    public static double GetSuccessRate<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Metrics?.SuccessRate ?? 0;
    }

    /// <summary>
    ///     Gets the failure rate as a percentage (0-100).
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="result">The workflow result</param>
    /// <returns>Failure rate percentage, or 0 if no steps were executed</returns>
    public static double GetFailureRate<TContext>(this WorkflowResult<TContext> result)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Metrics?.FailureRate ?? 0;
    }
}
