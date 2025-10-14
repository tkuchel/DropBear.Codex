#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Core;

/// <summary>
///     Abstract base class for implementing workflow steps with common functionality.
/// </summary>
public abstract class WorkflowStepBase<TContext> : IWorkflowStep<TContext> where TContext : class
{
    /// <summary>
    ///     Gets the name of this step. Defaults to the class name.
    /// </summary>
    public virtual string StepName => GetType().Name;

    /// <summary>
    ///     Gets a value indicating whether this step can be retried on failure.
    /// </summary>
    public virtual bool CanRetry => true;

    /// <summary>
    ///     Gets the maximum execution timeout for this step.
    /// </summary>
    public virtual TimeSpan? Timeout => null;

    /// <summary>
    ///     Executes this workflow step with the provided context.
    /// </summary>
    public abstract ValueTask<StepResult> ExecuteAsync(
        TContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Compensates (rolls back) this step's actions if the workflow fails.
    /// </summary>
    public virtual ValueTask<StepResult> CompensateAsync(
        TContext context,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(StepResult.Success());

    /// <summary>
    ///     Creates a successful step result with optional metadata.
    /// </summary>
    protected static StepResult Success(IReadOnlyDictionary<string, object>? metadata = null) =>
        StepResult.Success(metadata);

    /// <summary>
    ///     Creates a failed step result with an error message.
    /// </summary>
    protected static StepResult Failure(
        string message,
        bool shouldRetry = false,
        IReadOnlyDictionary<string, object>? metadata = null) =>
        StepResult.Failure(message, shouldRetry, metadata);

    /// <summary>
    ///     Creates a failed step result from an exception with full exception preservation.
    /// </summary>
    protected static StepResult Failure(
        Exception exception,
        bool shouldRetry = false,
        IReadOnlyDictionary<string, object>? metadata = null) =>
        StepResult.Failure(exception, shouldRetry, metadata);

    /// <summary>
    ///     Creates a failed step result from a DropBear.Codex.Core ResultError.
    /// </summary>
    protected static StepResult Failure(
        ResultError error,
        bool shouldRetry = false,
        IReadOnlyDictionary<string, object>? metadata = null) =>
        StepResult.FromError(error, shouldRetry, metadata);

    /// <summary>
    ///     Creates a suspension result that instructs the workflow to pause and wait for an external signal.
    /// </summary>
    protected static StepResult Suspend(
        string signalName,
        IReadOnlyDictionary<string, object>? metadata = null) =>
        StepResult.Suspend(signalName, metadata);
}
