using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

namespace DropBear.Codex.Workflow.Core;

/// <summary>
/// Abstract base class for implementing workflow steps with common functionality.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public abstract class WorkflowStepBase<TContext> : IWorkflowStep<TContext> where TContext : class
{
    /// <inheritdoc />
    public virtual string StepName => GetType().Name;

    /// <inheritdoc />
    public virtual bool CanRetry => true;

    /// <inheritdoc />
    public virtual TimeSpan? Timeout => null;

    /// <inheritdoc />
    public abstract ValueTask<StepResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual ValueTask<StepResult> CompensateAsync(TContext context, CancellationToken cancellationToken = default)
    {
        // Default implementation - no compensation required
        return ValueTask.FromResult(StepResult.Success());
    }

    /// <summary>
    /// Helper method to create successful results with optional metadata.
    /// </summary>
    /// <param name="metadata">Optional metadata to include</param>
    /// <returns>A successful step result</returns>
    protected static StepResult Success(IReadOnlyDictionary<string, object>? metadata = null) =>
        StepResult.Success(metadata);

    /// <summary>
    /// Helper method to create failure results.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="shouldRetry">Whether this step should be retried</param>
    /// <param name="metadata">Optional metadata to include</param>
    /// <returns>A failed step result</returns>
    protected static StepResult Failure(string message, bool shouldRetry = false, IReadOnlyDictionary<string, object>? metadata = null) =>
        StepResult.Failure(message, shouldRetry, metadata);

    /// <summary>
    /// Helper method to create failure results from exceptions.
    /// </summary>
    /// <param name="exception">The exception that caused the failure</param>
    /// <param name="shouldRetry">Whether this step should be retried</param>
    /// <param name="metadata">Optional metadata to include</param>
    /// <returns>A failed step result</returns>
    protected static StepResult Failure(Exception exception, bool shouldRetry = false, IReadOnlyDictionary<string, object>? metadata = null) =>
        StepResult.Failure(exception, shouldRetry, metadata);
}
