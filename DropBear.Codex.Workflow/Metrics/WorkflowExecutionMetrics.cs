namespace DropBear.Codex.Workflow.Metrics;

/// <summary>
/// Contains execution metrics for a completed workflow.
/// </summary>
public sealed record WorkflowExecutionMetrics
{
    /// <summary>
    /// Total time taken to execute the workflow.
    /// </summary>
    public required TimeSpan TotalExecutionTime { get; init; }

    /// <summary>
    /// Number of steps executed successfully.
    /// </summary>
    public required int StepsExecuted { get; init; }

    /// <summary>
    /// Number of steps that failed.
    /// </summary>
    public required int StepsFailed { get; init; }

    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public required int RetryAttempts { get; init; }

    /// <summary>
    /// Peak memory usage during execution (if available).
    /// </summary>
    public long? PeakMemoryUsage { get; init; }

    /// <summary>
    /// Custom metrics collected during execution.
    /// </summary>
    public IReadOnlyDictionary<string, object>? CustomMetrics { get; init; }
}
