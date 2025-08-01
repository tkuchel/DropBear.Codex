namespace DropBear.Codex.Workflow.Metrics;

/// <summary>
/// Represents a single step execution in the workflow trace.
/// </summary>
public sealed record StepExecutionTrace
{
    /// <summary>
    /// The name of the executed step.
    /// </summary>
    public required string StepName { get; init; }

    /// <summary>
    /// When the step started executing.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// When the step finished executing.
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// The result of step execution.
    /// </summary>
    public required Results.StepResult Result { get; init; }

    /// <summary>
    /// Number of retry attempts for this step.
    /// </summary>
    public required int RetryAttempts { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; init; }
}
