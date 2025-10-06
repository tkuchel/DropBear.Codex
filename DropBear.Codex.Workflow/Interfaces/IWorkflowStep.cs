using System.Diagnostics.CodeAnalysis;

namespace DropBear.Codex.Workflow.Interfaces;

/// <summary>
/// Represents a single executable step within a workflow.
/// </summary>
/// <typeparam name="TContext">The type of context that flows through the workflow</typeparam>
public interface IWorkflowStep<TContext> where TContext : class
{
    /// <summary>
    /// Gets the unique name identifier for this step.
    /// Used for logging, monitoring, and workflow visualization.
    /// </summary>
    string StepName { get; }

    /// <summary>
    /// Indicates whether this step can be safely retried on failure.
    /// Steps that are not idempotent should return false.
    /// </summary>
    bool CanRetry { get; }

    /// <summary>
    /// Gets the maximum execution timeout for this step.
    /// Returns null for no timeout.
    /// </summary>
    TimeSpan? Timeout { get; }

    /// <summary>
    /// Executes the step logic asynchronously.
    /// </summary>
    /// <param name="context">The workflow context containing shared state</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The result of step execution</returns>
    ValueTask<Results.StepResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional compensation logic for rollback scenarios.
    /// Called when the workflow needs to undo this step's changes.
    /// </summary>
    /// <param name="context">The workflow context</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The result of compensation execution</returns>
    ValueTask<Results.StepResult> CompensateAsync(TContext context, CancellationToken cancellationToken = default)
    {
        // Default implementation - no compensation required
        return ValueTask.FromResult(Results.StepResult.Success());
    }
}
