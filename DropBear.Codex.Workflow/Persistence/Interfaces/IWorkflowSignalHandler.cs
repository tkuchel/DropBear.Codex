#region

using DropBear.Codex.Workflow.Persistence.Models;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Interfaces;

/// <summary>
///     Handles workflow signal delivery and validation.
/// </summary>
public interface IWorkflowSignalHandler
{
    /// <summary>
    ///     Delivers a signal to a workflow instance and resumes execution.
    /// </summary>
    /// <typeparam name="TData">The type of signal data</typeparam>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="signalName">The name of the signal</param>
    /// <param name="signalData">Optional signal data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the signal was delivered and workflow resumed, false otherwise</returns>
    ValueTask<bool> DeliverSignalAsync<TData>(
        string workflowInstanceId,
        string signalName,
        TData? signalData,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Checks if a workflow is in a valid state to receive the specified signal.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance ID</param>
    /// <param name="signalName">The name of the signal</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the workflow can receive the signal, false otherwise</returns>
    ValueTask<bool> IsWaitingForSignalAsync(
        string workflowInstanceId,
        string signalName,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Validates that a workflow state is eligible for signaling.
    /// </summary>
    /// <param name="stateInfo">The workflow state information</param>
    /// <param name="signalName">The expected signal name</param>
    /// <returns>True if the state is valid for the signal, false otherwise</returns>
    bool ValidateStateForSignaling(WorkflowStateInfo stateInfo, string signalName);
}
