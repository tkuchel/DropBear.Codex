// File: DropBear.Codex.Workflow/Persistence/Models/WorkflowInstanceState.cs

#region

using DropBear.Codex.Workflow.Common;
using DropBear.Codex.Workflow.Metrics;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Models;

/// <summary>
///     Represents the persisted state of a workflow instance.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public sealed record WorkflowInstanceState<TContext> where TContext : class
{
    /// <summary>
    ///     Gets or sets the unique identifier for this workflow instance.
    /// </summary>
    public required string WorkflowInstanceId { get; init; }

    /// <summary>
    ///     Gets or sets the workflow definition identifier.
    /// </summary>
    public required string WorkflowId { get; init; }

    /// <summary>
    ///     Gets or sets the display name of the workflow.
    /// </summary>
    public required string WorkflowDisplayName { get; init; }

    /// <summary>
    ///     Gets or sets the current workflow context.
    /// </summary>
    public required TContext Context { get; init; }

    /// <summary>
    ///     Gets or sets the current status of the workflow.
    /// </summary>
    public required WorkflowStatus Status { get; init; }

    /// <summary>
    ///     Gets or sets when the workflow instance was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    ///     Gets or sets when the workflow instance was last updated.
    /// </summary>
    public required DateTimeOffset LastUpdatedAt { get; init; }

    /// <summary>
    ///     Gets or sets the signal name the workflow is waiting for (if suspended).
    /// </summary>
    public string? WaitingForSignal { get; init; }

    /// <summary>
    ///     Gets or sets when the signal timeout occurs (if waiting for a signal).
    /// </summary>
    public DateTimeOffset? SignalTimeoutAt { get; init; }

    /// <summary>
    ///     Gets or sets the serialized workflow definition for resumption.
    /// </summary>
    public string? SerializedWorkflowDefinition { get; init; }

    /// <summary>
    ///     Gets or sets the execution history of the workflow.
    /// </summary>
    public IList<StepExecutionTrace> ExecutionHistory { get; init; } = [];

    /// <summary>
    ///     Gets or sets additional metadata about the workflow instance.
    /// </summary>
    public IDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets or sets the correlation ID for tracking related operations.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    ///     Gets or sets the completion time (if completed).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    ///     Gets or sets the error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }

    // ============ NEW PROPERTIES FOR TYPE INFORMATION ============

    /// <summary>
    ///     Gets or sets the fully qualified name of the context type.
    ///     Used for type-safe deserialization without discovery.
    /// </summary>
    public string? ContextTypeName { get; init; }

    /// <summary>
    ///     Gets or sets the assembly qualified name of the context type.
    ///     Used for type-safe deserialization without discovery.
    /// </summary>
    public string? ContextAssemblyQualifiedName { get; init; }
}
