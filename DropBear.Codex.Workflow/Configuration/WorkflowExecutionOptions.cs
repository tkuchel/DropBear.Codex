namespace DropBear.Codex.Workflow.Configuration;

/// <summary>
///     Options for controlling workflow execution behavior.
/// </summary>
public sealed class WorkflowExecutionOptions
{
    /// <summary>
    ///     Gets or sets the correlation ID for tracking related operations.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether compensation should be executed on failure.
    /// </summary>
    public bool EnableCompensation { get; set; }

    /// <summary>
    ///     Gets or sets the retry policy for workflow steps.
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;

    /// <summary>
    ///     Gets or sets the maximum workflow execution timeout.
    ///     If not set, uses the workflow definition's timeout.
    /// </summary>
    public TimeSpan? WorkflowTimeout { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to collect detailed metrics.
    /// </summary>
    public bool EnableDetailedMetrics { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether to enable distributed tracing.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    ///     Gets or sets additional metadata for the workflow execution.
    /// </summary>
    public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets or sets the maximum degree of parallelism for parallel nodes.
    /// </summary>
    public int? MaxDegreeOfParallelism { get; set; }
}
