#region

using DropBear.Codex.Workflow.Persistence.Models;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Interfaces;

/// <summary>
///     Repository interface for persisting workflow state
/// </summary>
public interface IWorkflowStateRepository
{
    ValueTask<string> SaveWorkflowStateAsync<TContext>(WorkflowInstanceState<TContext> state,
        CancellationToken cancellationToken = default) where TContext : class;

    ValueTask<WorkflowInstanceState<TContext>?> GetWorkflowStateAsync<TContext>(string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class;

    ValueTask UpdateWorkflowStateAsync<TContext>(WorkflowInstanceState<TContext> state,
        CancellationToken cancellationToken = default) where TContext : class;

    ValueTask DeleteWorkflowStateAsync(string workflowInstanceId, CancellationToken cancellationToken = default);

    ValueTask<IEnumerable<WorkflowInstanceState<TContext>>> GetWaitingWorkflowsAsync<TContext>(
        string? signalName = null, CancellationToken cancellationToken = default) where TContext : class;

    ValueTask<(string? AssemblyQualifiedName, string? TypeName)> GetWorkflowContextTypeInfoAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken = default);
}
