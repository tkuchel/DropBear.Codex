#region

using DropBear.Codex.Workflow.Common;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Models;

public sealed class WorkflowStateBasicInfo
{
    public required string WorkflowInstanceId { get; init; }
    public required WorkflowStatus Status { get; init; }
    public string? WaitingForSignal { get; init; }
    public required string ContextTypeName { get; init; }
}
