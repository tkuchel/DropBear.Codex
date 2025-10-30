#region

using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using DropBear.Codex.Workflow.Persistence.Models;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Implementation;

/// <summary>
///     Default implementation of workflow signal handling.
/// </summary>
public sealed partial class DefaultWorkflowSignalHandler : IWorkflowSignalHandler
{
    private readonly ILogger<DefaultWorkflowSignalHandler> _logger;
    private readonly Func<string, Type, CancellationToken, ValueTask<bool>> _resumeWorkflowCallback;
    private readonly IWorkflowStateCoordinator _stateCoordinator;

    /// <summary>
    ///     Initializes a new instance of the signal handler.
    /// </summary>
    /// <param name="stateCoordinator">State coordinator for state retrieval</param>
    /// <param name="resumeWorkflowCallback">Callback to resume workflow execution</param>
    /// <param name="logger">Logger instance</param>
    public DefaultWorkflowSignalHandler(
        IWorkflowStateCoordinator stateCoordinator,
        Func<string, Type, CancellationToken, ValueTask<bool>> resumeWorkflowCallback,
        ILogger<DefaultWorkflowSignalHandler> logger)
    {
        _stateCoordinator = stateCoordinator ?? throw new ArgumentNullException(nameof(stateCoordinator));
        _resumeWorkflowCallback = resumeWorkflowCallback ?? throw new ArgumentNullException(nameof(resumeWorkflowCallback));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<bool> DeliverSignalAsync<TData>(
        string workflowInstanceId,
        string signalName,
        TData? signalData,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        LogSignalingWorkflow(workflowInstanceId, signalName);

        // Get workflow state and context type
        (WorkflowStateInfo? stateInfo, Type? contextType) = await _stateCoordinator
            .GetWorkflowStateInfoAsync(workflowInstanceId, cancellationToken)
            .ConfigureAwait(false);

        if (stateInfo is null || contextType is null)
        {
            LogWorkflowNotFoundForSignaling(workflowInstanceId);
            return false;
        }

        // Validate state is eligible for signaling
        if (!ValidateStateForSignaling(stateInfo, signalName))
        {
            return false;
        }

        // Delegate to resume callback
        return await _resumeWorkflowCallback(workflowInstanceId, contextType, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<bool> IsWaitingForSignalAsync(
        string workflowInstanceId,
        string signalName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        (WorkflowStateInfo? stateInfo, _) = await _stateCoordinator
            .GetWorkflowStateInfoAsync(workflowInstanceId, cancellationToken)
            .ConfigureAwait(false);

        if (stateInfo is null)
        {
            return false;
        }

        return ValidateStateForSignaling(stateInfo, signalName);
    }

    public bool ValidateStateForSignaling(WorkflowStateInfo stateInfo, string signalName)
    {
        ArgumentNullException.ThrowIfNull(stateInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        // Check if workflow is in a waiting state
        if (stateInfo.Status != WorkflowStatus.WaitingForSignal &&
            stateInfo.Status != WorkflowStatus.WaitingForApproval)
        {
            LogWorkflowNotWaitingForSignal(stateInfo.WorkflowInstanceId, stateInfo.Status);
            return false;
        }

        // Check if signal name matches
        if (!string.Equals(stateInfo.WaitingForSignal, signalName, StringComparison.OrdinalIgnoreCase))
        {
            LogSignalMismatch(stateInfo.WorkflowInstanceId, stateInfo.WaitingForSignal, signalName);
            return false;
        }

        return true;
    }

    #region Logging

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Signaling workflow {WorkflowInstanceId} with signal: {SignalName}")]
    partial void LogSignalingWorkflow(string workflowInstanceId, string signalName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workflow {WorkflowInstanceId} not found for signaling")]
    partial void LogWorkflowNotFoundForSignaling(string workflowInstanceId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Workflow {WorkflowInstanceId} is not waiting for signal. Current status: {Status}")]
    partial void LogWorkflowNotWaitingForSignal(string workflowInstanceId, WorkflowStatus status);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Signal mismatch for workflow {WorkflowInstanceId}. Expected: {ExpectedSignal}, Got: {ReceivedSignal}")]
    partial void LogSignalMismatch(string workflowInstanceId, string? expectedSignal, string receivedSignal);

    #endregion
}
