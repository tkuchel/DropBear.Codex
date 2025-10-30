#region

using DropBear.Codex.Core.Results.Async;
using DropBear.Codex.Workflow.Errors;
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

    /// <summary>
    ///     Streams waiting workflow instances of the specified context type asynchronously.
    ///     This method is more memory-efficient than <see cref="GetWaitingWorkflowsAsync{TContext}" />
    ///     for repositories containing thousands of workflow instances.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type.</typeparam>
    /// <param name="signalName">Optional signal name to filter by. If null, all waiting workflows are returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///     An <see cref="AsyncEnumerableResult{T, TError}" /> that yields waiting workflow instances incrementally.
    /// </returns>
    AsyncEnumerableResult<WorkflowInstanceState<TContext>, WorkflowExecutionError> StreamWaitingWorkflowsAsync<TContext>(
        string? signalName = null, CancellationToken cancellationToken = default) where TContext : class;
}
