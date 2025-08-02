namespace DropBear.Codex.Workflow.Persistence.Models;

/// <summary>
/// Result of persistent workflow operations
/// </summary>
public sealed record PersistentWorkflowResult<TContext> where TContext : class
{
    public required string WorkflowInstanceId { get; init; }
    public required WorkflowStatus Status { get; init; }
    public required TContext Context { get; init; }
    public bool IsCompleted => Status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled;
    public bool IsWaiting => Status is WorkflowStatus.WaitingForSignal or WorkflowStatus.WaitingForApproval or WorkflowStatus.Suspended;
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
    public DropBear.Codex.Workflow.Results.WorkflowResult<TContext>? CompletionResult { get; init; }
    public string? WaitingForSignal { get; init; }
    public DateTimeOffset? SignalTimeoutAt { get; init; }
}
