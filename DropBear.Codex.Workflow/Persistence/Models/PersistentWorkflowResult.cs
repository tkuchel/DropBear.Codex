#region

using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Models;

/// <summary>
///     Represents the result of a persistent workflow execution.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed class PersistentWorkflowResult<TContext> where TContext : class
{
    /// <summary>
    ///     Gets or sets the unique identifier of the workflow instance.
    /// </summary>
    public required string WorkflowInstanceId { get; init; }

    /// <summary>
    ///     Gets or sets the current status of the workflow.
    /// </summary>
    public required WorkflowStatus Status { get; init; }

    /// <summary>
    ///     Gets or sets the current workflow context.
    /// </summary>
    public required TContext Context { get; init; }

    /// <summary>
    ///     Gets or sets the signal name the workflow is waiting for (if suspended).
    /// </summary>
    public string? WaitingForSignal { get; init; }

    /// <summary>
    ///     Gets or sets when the signal timeout occurs (if waiting).
    /// </summary>
    public DateTimeOffset? SignalTimeoutAt { get; init; }

    /// <summary>
    ///     Gets or sets the error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    ///     Gets or sets the exception that caused the failure (if any).
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    ///     Gets or sets the completion result (if completed).
    /// </summary>
    public WorkflowResult<TContext>? CompletionResult { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the workflow is still running.
    /// </summary>
    public bool IsRunning => Status is WorkflowStatus.Running or WorkflowStatus.WaitingForSignal
        or WorkflowStatus.WaitingForApproval;

    /// <summary>
    ///     Gets a value indicating whether the workflow completed successfully.
    /// </summary>
    public bool IsCompleted => Status == WorkflowStatus.Completed;

    /// <summary>
    ///     Gets a value indicating whether the workflow failed.
    /// </summary>
    public bool IsFailed => Status == WorkflowStatus.Failed;

    /// <summary>
    ///     Gets a value indicating whether the workflow is waiting for input.
    /// </summary>
    public bool IsWaiting => Status is WorkflowStatus.WaitingForSignal or WorkflowStatus.WaitingForApproval;
}
