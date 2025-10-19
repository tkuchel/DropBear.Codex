using DropBear.Codex.Workflow.Results;

namespace DropBear.Codex.Workflow.Metrics;

/// <summary>
///     Represents a single step execution in the workflow trace with comprehensive timing and result information.
/// </summary>
public sealed record StepExecutionTrace
{
    /// <summary>
    ///     Gets the name of the executed step.
    /// </summary>
    public required string StepName { get; init; }

    /// <summary>
    ///     Gets the node ID of the executed step for detailed tracing.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    ///     Gets when the step started executing (UTC).
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    ///     Gets when the step finished executing (UTC).
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    ///     Gets the total execution time for this step.
    ///     Calculated as the difference between EndTime and StartTime.
    /// </summary>
    public TimeSpan ExecutionTime => EndTime - StartTime;

    /// <summary>
    ///     Gets the result of step execution.
    /// </summary>
    public required StepResult Result { get; init; }

    /// <summary>
    ///     Gets the number of retry attempts made for this step.
    ///     0 indicates the step succeeded on the first attempt.
    /// </summary>
    public required int RetryAttempts { get; init; }

    /// <summary>
    ///     Gets the correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    ///     Gets additional metadata collected during step execution.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    // ===== NEW PROPERTY FOR COMPENSATION SUPPORT =====
    /// <summary>
    ///     Gets the type information for the step that was executed.
    ///     Used for compensation to reconstruct the step instance.
    /// </summary>
    public Type? StepType { get; init; }

    /// <summary>
    ///     Gets the type of the workflow context.
    ///     Used for compensation to call the correctly typed CompensateAsync method.
    /// </summary>
    public Type? ContextType { get; init; }
    // =================================================

    /// <summary>
    ///     Gets a value indicating whether this step was retried.
    /// </summary>
    public bool WasRetried => RetryAttempts > 0;

    /// <summary>
    ///     Gets a value indicating whether this step succeeded.
    /// </summary>
    public bool Succeeded => Result.IsSuccess;

    /// <summary>
    ///     Gets a value indicating whether this step failed.
    /// </summary>
    public bool Failed => !Result.IsSuccess;

    /// <summary>
    ///     Creates a formatted summary string for logging.
    /// </summary>
    /// <returns>A human-readable summary of the step execution</returns>
    public string ToSummaryString()
    {
        string status = Result.IsSuccess ? "✓" : "✗";
        string retryInfo = RetryAttempts > 0 ? $" (retried {RetryAttempts}x)" : "";
        return $"{status} {StepName}: {ExecutionTime.TotalMilliseconds:F2}ms{retryInfo}";
    }
}
