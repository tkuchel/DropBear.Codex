using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;

namespace DropBear.Codex.Workflow.Persistence.Interfaces;

/// <summary>
/// Extended workflow engine that supports long-running, persistent workflows
/// </summary>
public interface IPersistentWorkflowEngine : IWorkflowEngine
{
    /// <summary>
    /// Starts a persistent workflow that can be suspended and resumed
    /// </summary>
    ValueTask<PersistentWorkflowResult<TContext>> StartPersistentWorkflowAsync<TContext>(
        IWorkflowDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default) where TContext : class;

    /// <summary>
    /// Resumes a suspended workflow from its saved state
    /// </summary>
    ValueTask<PersistentWorkflowResult<TContext>> ResumeWorkflowAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class;

    /// <summary>
    /// Signals a waiting workflow to continue execution
    /// </summary>
    ValueTask<bool> SignalWorkflowAsync<TData>(
        string workflowInstanceId,
        string signalName,
        TData? signalData = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of a persistent workflow
    /// </summary>
    ValueTask<WorkflowInstanceState<TContext>?> GetWorkflowStateAsync<TContext>(
        string workflowInstanceId,
        CancellationToken cancellationToken = default) where TContext : class;

    /// <summary>
    /// Cancels a running or suspended workflow
    /// </summary>
    ValueTask<bool> CancelWorkflowAsync(
        string workflowInstanceId,
        string reason,
        CancellationToken cancellationToken = default);
}
