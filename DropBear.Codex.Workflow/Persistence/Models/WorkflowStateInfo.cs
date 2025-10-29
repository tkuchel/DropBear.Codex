#region

using DropBear.Codex.Workflow.Common;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Models;

/// <summary>
///     Lightweight workflow state information for querying without full context deserialization.
/// </summary>
public sealed record WorkflowStateInfo
{
    /// <summary>
    ///     Gets the workflow instance ID.
    /// </summary>
    public required string WorkflowInstanceId { get; init; }

    /// <summary>
    ///     Gets the current workflow status.
    /// </summary>
    public required WorkflowStatus Status { get; init; }

    /// <summary>
    ///     Gets the name of the signal the workflow is waiting for, if any.
    /// </summary>
    public string? WaitingForSignal { get; init; }
}
