namespace DropBear.Codex.Workflow.Results;

/// <summary>
/// Represents the final result of workflow execution.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed record WorkflowResult<TContext> where TContext : class
{
    /// <summary>
    /// Indicates whether the workflow completed successfully.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// The final state of the workflow context.
    /// </summary>
    public required TContext Context { get; init; }

    /// <summary>
    /// Error message if the workflow failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception that caused the workflow failure, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Execution metrics and metadata.
    /// </summary>
    public Metrics.WorkflowExecutionMetrics? Metrics { get; init; }

    /// <summary>
    /// Detailed execution trace for debugging and monitoring.
    /// </summary>
    public IReadOnlyList<Metrics.StepExecutionTrace>? ExecutionTrace { get; init; }
}
