using DropBear.Codex.Workflow.Core;
using DropBear.Codex.Workflow.Results;

namespace DropBear.Codex.Workflow.Persistence.Steps;

/// <summary>
/// Workflow step that commits previously stored pending changes
/// </summary>
/// <typeparam name="TContext">The workflow context type</typeparam>
public abstract class CommitChangesStep<TContext> : WorkflowStepBase<TContext> where TContext : class
{
    public override async ValueTask<StepResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Retrieve and commit the pending changes
            var pendingChanges = await GetPendingChangesAsync(context, cancellationToken);
            if (pendingChanges == null || !pendingChanges.Any())
            {
                return Success(new Dictionary<string, object> { ["NoPendingChanges"] = true });
            }

            await CommitPendingChangesAsync(context, pendingChanges, cancellationToken);

            // Clean up pending changes after successful commit
            await ClearPendingChangesAsync(context, cancellationToken);

            var metadata = new Dictionary<string, object>
            {
                ["CommittedAt"] = DateTimeOffset.UtcNow,
                ["ChangesCommitted"] = pendingChanges.Count
            };

            return Success(metadata);
        }
        catch (Exception ex)
        {
            return Failure($"Failed to commit changes: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Retrieves pending changes for this context
    /// </summary>
    protected abstract ValueTask<Dictionary<string, object>?> GetPendingChangesAsync(TContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the pending changes to the persistent store
    /// </summary>
    protected abstract ValueTask CommitPendingChangesAsync(TContext context, Dictionary<string, object> pendingChanges, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the pending changes after successful commit
    /// </summary>
    protected abstract ValueTask ClearPendingChangesAsync(TContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compensation logic - rollback committed changes if possible
    /// </summary>
    public override async ValueTask<StepResult> CompensateAsync(TContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await RollbackCommittedChangesAsync(context, cancellationToken);
            return Success();
        }
        catch (Exception ex)
        {
            return Failure($"Failed to rollback changes: {ex.Message}");
        }
    }

    /// <summary>
    /// Rolls back committed changes during compensation
    /// </summary>
    protected abstract ValueTask RollbackCommittedChangesAsync(TContext context, CancellationToken cancellationToken = default);
}
