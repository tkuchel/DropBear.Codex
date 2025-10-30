#region

using DropBear.Codex.Workflow.Persistence.Models;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Interfaces;

/// <summary>
///     Coordinates workflow state persistence and retrieval operations.
/// </summary>
public interface IWorkflowStateCoordinator
{
    /// <summary>
    ///     Saves workflow state to persistent storage.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="state">The workflow state to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask SaveWorkflowStateAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        CancellationToken cancellationToken) where TContext : class;

    /// <summary>
    ///     Loads workflow state from persistent storage.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The workflow state, or null if not found</returns>
    ValueTask<WorkflowInstanceState<TContext>?> LoadWorkflowStateAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken) where TContext : class;

    /// <summary>
    ///     Updates existing workflow state in persistent storage.
    /// </summary>
    /// <typeparam name="TContext">The workflow context type</typeparam>
    /// <param name="state">The updated workflow state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    ValueTask UpdateWorkflowStateAsync<TContext>(
        WorkflowInstanceState<TContext> state,
        CancellationToken cancellationToken) where TContext : class;

    /// <summary>
    ///     Gets workflow state information without requiring generic type parameter.
    ///     Uses type resolution to determine the context type.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Workflow state information and the resolved context type</returns>
    ValueTask<(WorkflowStateInfo? StateInfo, Type? ContextType)> GetWorkflowStateInfoAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Gets the context type for a workflow instance.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The context type, or null if not found</returns>
    ValueTask<Type?> GetWorkflowContextTypeAsync(
        string workflowInstanceId,
        CancellationToken cancellationToken);
}
