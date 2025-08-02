using DropBear.Codex.Workflow.Persistence.Models;
using DropBear.Codex.Workflow.Results;

namespace DropBear.Codex.Workflow.Persistence.Steps;

/// <summary>
/// Workflow step that requests approval and waits for a response
/// </summary>
/// <typeparam name="TContext">The workflow context type</typeparam>
public abstract class WaitForApprovalStep<TContext> : WaitForSignalStep<TContext, ApprovalResponse>
    where TContext : class
{
    public override string SignalName => $"approval_{GetType().Name}_{GetHashCode()}";
    public override TimeSpan? SignalTimeout => TimeSpan.FromDays(7); // Default 7-day approval timeout

    /// <summary>
    /// Creates the approval request that will be sent to approvers
    /// </summary>
    /// <param name="context">The workflow context</param>
    /// <returns>The approval request details</returns>
    public abstract ApprovalRequest CreateApprovalRequest(TContext context);

    /// <summary>
    /// Validates the approval response and updates the context
    /// </summary>
    public override async ValueTask<StepResult> ProcessSignalAsync(
        TContext context,
        ApprovalResponse? approvalResponse,
        CancellationToken cancellationToken = default)
    {
        if (approvalResponse == null)
        {
            return Failure("No approval response received");
        }

        // Update context with approval decision
        await OnApprovalReceivedAsync(context, approvalResponse, cancellationToken);

        var metadata = new Dictionary<string, object>
        {
            ["ApprovalDecision"] = approvalResponse.IsApproved,
            ["ApprovedBy"] = approvalResponse.ApprovedBy,
            ["ApprovedAt"] = approvalResponse.ApprovedAt,
            ["ApprovalComments"] = approvalResponse.Comments ?? ""
        };

        return approvalResponse.IsApproved
            ? Success(metadata)
            : Failure($"Approval denied by {approvalResponse.ApprovedBy}: {approvalResponse.Comments}", false, metadata);
    }

    /// <summary>
    /// Called when approval response is received to update the context
    /// </summary>
    protected virtual ValueTask OnApprovalReceivedAsync(TContext context, ApprovalResponse approvalResponse, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
