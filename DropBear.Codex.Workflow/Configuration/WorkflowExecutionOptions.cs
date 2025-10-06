namespace DropBear.Codex.Workflow.Configuration;

/// <summary>
/// Configuration options for workflow execution.
/// </summary>
public sealed record WorkflowExecutionOptions
{
    /// <summary>
    /// Maximum number of retry attempts for failed steps.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Base delay for exponential backoff retry strategy.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum delay between retry attempts.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether to collect detailed execution traces.
    /// Disable for performance in production scenarios.
    /// </summary>
    public bool EnableExecutionTracing { get; init; } = true;

    /// <summary>
    /// Whether to collect memory usage metrics.
    /// May impact performance.
    /// </summary>
    public bool EnableMemoryMetrics { get; init; } = false;

    /// <summary>
    /// Maximum degree of parallelism for parallel steps.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;

    /// <summary>
    /// Custom execution context for advanced scenarios.
    /// </summary>
    public IReadOnlyDictionary<string, object>? CustomOptions { get; init; }

    /// <summary>
    /// Gets the default execution options.
    /// </summary>
    public static WorkflowExecutionOptions Default { get; } = new();
}
