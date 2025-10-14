namespace DropBear.Codex.Workflow.Metrics;

/// <summary>
///     Contains execution metrics for a completed workflow run.
/// </summary>
public sealed record WorkflowMetrics
{
    /// <summary>
    ///     Gets the total time taken to execute the workflow.
    /// </summary>
    public required TimeSpan TotalExecutionTime { get; init; }

    /// <summary>
    ///     Gets the total number of steps that were executed (successful + failed).
    /// </summary>
    public required int StepsExecuted { get; init; }

    /// <summary>
    ///     Gets the number of steps that completed successfully.
    /// </summary>
    public required int StepsSucceeded { get; init; }

    /// <summary>
    ///     Gets the number of steps that failed during execution.
    /// </summary>
    public required int StepsFailed { get; init; }

    /// <summary>
    ///     Gets the total number of retry attempts across all steps.
    /// </summary>
    public required int TotalRetries { get; init; }

    /// <summary>
    ///     Gets the average execution time per step.
    ///     Returns TimeSpan.Zero if no steps were executed.
    /// </summary>
    public required TimeSpan AverageStepExecutionTime { get; init; }

    /// <summary>
    ///     Gets the peak memory usage during workflow execution (if metrics collection is enabled).
    /// </summary>
    public long? PeakMemoryUsage { get; init; }

    /// <summary>
    ///     Gets custom metrics that were collected during workflow execution.
    /// </summary>
    public IReadOnlyDictionary<string, object>? CustomMetrics { get; init; }

    /// <summary>
    ///     Gets the memory used at the start of workflow execution (if metrics collection is enabled).
    /// </summary>
    public long? StartMemoryUsage { get; init; }

    /// <summary>
    ///     Gets the memory used at the end of workflow execution (if metrics collection is enabled).
    /// </summary>
    public long? EndMemoryUsage { get; init; }

    /// <summary>
    ///     Calculates the total memory delta (end - start) if both values are available.
    /// </summary>
    public long? MemoryDelta =>
        StartMemoryUsage.HasValue && EndMemoryUsage.HasValue
            ? EndMemoryUsage.Value - StartMemoryUsage.Value
            : null;

    /// <summary>
    ///     Gets the success rate as a percentage (0-100).
    ///     Returns 0 if no steps were executed.
    /// </summary>
    public double SuccessRate =>
        StepsExecuted > 0
            ? (double)StepsSucceeded / StepsExecuted * 100
            : 0;

    /// <summary>
    ///     Gets the failure rate as a percentage (0-100).
    ///     Returns 0 if no steps were executed.
    /// </summary>
    public double FailureRate =>
        StepsExecuted > 0
            ? (double)StepsFailed / StepsExecuted * 100
            : 0;

    /// <summary>
    ///     Gets the average number of retries per step.
    ///     Returns 0 if no steps were executed.
    /// </summary>
    public double AverageRetriesPerStep =>
        StepsExecuted > 0
            ? (double)TotalRetries / StepsExecuted
            : 0;

    /// <summary>
    ///     Creates a metrics summary as a formatted string for logging.
    /// </summary>
    /// <returns>A human-readable summary of the workflow metrics</returns>
    public string ToSummaryString()
    {
        return $"Workflow Metrics: " +
               $"Total Time: {TotalExecutionTime.TotalMilliseconds:F2}ms, " +
               $"Steps: {StepsSucceeded}/{StepsExecuted} succeeded, " +
               $"Retries: {TotalRetries}, " +
               $"Avg Step Time: {AverageStepExecutionTime.TotalMilliseconds:F2}ms" +
               (PeakMemoryUsage.HasValue ? $", Peak Memory: {PeakMemoryUsage.Value / 1024.0 / 1024.0:F2}MB" : "");
    }
}
