using DropBear.Codex.Workflow.Persistence.Models;
using DropBear.Codex.Workflow.Results;

namespace DropBear.Codex.Workflow.Persistence.Interfaces;

/// <summary>
/// Service for sending notifications about workflow events
/// </summary>
public interface IWorkflowNotificationService
{
    ValueTask SendApprovalRequestAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        ApprovalRequest approvalRequest,
        CancellationToken cancellationToken = default) where TContext : class;

    ValueTask SendWorkflowCompletionNotificationAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        WorkflowResult<TContext> result,
        CancellationToken cancellationToken = default) where TContext : class;

    ValueTask SendWorkflowErrorNotificationAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        string errorMessage,
        Exception? exception = null,
        CancellationToken cancellationToken = default) where TContext : class;
}
