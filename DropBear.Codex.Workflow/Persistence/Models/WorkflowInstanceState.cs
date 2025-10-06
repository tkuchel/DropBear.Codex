namespace DropBear.Codex.Workflow.Persistence.Models;

/// <summary>
/// Represents the persistent state of a workflow instance
/// </summary>
public sealed record WorkflowInstanceState<TContext> where TContext : class
{
    public required string WorkflowInstanceId { get; init; }
    public required string WorkflowId { get; init; }
    public required string WorkflowDisplayName { get; init; }
    public required TContext Context { get; init; }
    public required WorkflowStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset LastUpdatedAt { get; init; }
    public string? CurrentStepId { get; init; }
    public string? WaitingForSignal { get; init; }
    public DateTimeOffset? SignalTimeoutAt { get; init; }
    public string? CreatedBy { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public List<WorkflowExecutionCheckpoint> ExecutionHistory { get; init; } = new();
    public Dictionary<string, object> PendingChanges { get; init; } = new();
    public string? SerializedWorkflowDefinition { get; init; }
}

/// <summary>
/// Status of a workflow instance
/// </summary>
public enum WorkflowStatus
{
    Running,
    Suspended,
    WaitingForSignal,
    WaitingForApproval,
    Completed,
    Failed,
    Cancelled,
    TimedOut
}

/// <summary>
/// Represents a checkpoint in workflow execution
/// </summary>
public sealed record WorkflowExecutionCheckpoint
{
    public required string StepId { get; init; }
    public required string StepName { get; init; }
    public required DateTimeOffset ExecutedAt { get; init; }
    public required bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> StepMetadata { get; init; } = new();
}
