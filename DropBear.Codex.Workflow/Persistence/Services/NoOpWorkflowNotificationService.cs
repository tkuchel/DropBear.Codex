#region

using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Services;

/// <summary>
///     No-op implementation of workflow notification service for when notifications are not needed.
/// </summary>
internal sealed class NoOpWorkflowNotificationService : IWorkflowNotificationService
{
    public ValueTask SendApprovalRequestAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        ApprovalRequest approvalRequest,
        CancellationToken cancellationToken = default)
        where TContext : class =>
        ValueTask.CompletedTask;

    public ValueTask SendWorkflowCompletionNotificationAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        WorkflowResult<TContext> result,
        CancellationToken cancellationToken = default)
        where TContext : class =>
        ValueTask.CompletedTask;

    public ValueTask SendWorkflowErrorNotificationAsync<TContext>(
        WorkflowInstanceState<TContext> workflowState,
        string errorMessage,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
        where TContext : class =>
        ValueTask.CompletedTask;
}
