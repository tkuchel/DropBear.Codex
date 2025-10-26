#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Workflow.Errors;

/// <summary>
///     Represents errors that occur during workflow execution.
///     Use this instead of throwing WorkflowExecutionException.
/// </summary>
public sealed record WorkflowExecutionError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="WorkflowExecutionError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public WorkflowExecutionError(string message) : base(message) { }

    /// <summary>
    ///     Gets or sets the workflow ID that failed.
    /// </summary>
    public string? WorkflowId { get; init; }

    /// <summary>
    ///     Gets or sets the correlation ID for the failed execution.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    ///     Gets or sets the step name where the failure occurred.
    /// </summary>
    public string? StepName { get; init; }

    #region Factory Methods

    /// <summary>
    ///     Creates an error for a failed workflow step.
    /// </summary>
    /// <param name="stepName">The name of the step that failed.</param>
    /// <param name="reason">The reason for the failure.</param>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>A new <see cref="WorkflowExecutionError"/> instance.</returns>
    public static WorkflowExecutionError StepFailed(
        string stepName,
        string reason,
        string? workflowId = null,
        string? correlationId = null)
    {
        return new WorkflowExecutionError($"Step '{stepName}' failed: {reason}")
        {
            StepName = stepName,
            WorkflowId = workflowId,
            CorrelationId = correlationId,
            Code = "WF_STEP_FAILED",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for a workflow execution failure.
    /// </summary>
    /// <param name="workflowId">The workflow ID that failed.</param>
    /// <param name="reason">The reason for the failure.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>A new <see cref="WorkflowExecutionError"/> instance.</returns>
    public static WorkflowExecutionError ExecutionFailed(
        string workflowId,
        string reason,
        string? correlationId = null)
    {
        return new WorkflowExecutionError($"Workflow '{workflowId}' execution failed: {reason}")
        {
            WorkflowId = workflowId,
            CorrelationId = correlationId,
            Code = "WF_EXEC_FAILED",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.Critical
        };
    }

    /// <summary>
    ///     Creates an error for a compensation failure.
    /// </summary>
    /// <param name="stepName">The name of the step that failed compensation.</param>
    /// <param name="reason">The reason for the compensation failure.</param>
    /// <param name="workflowId">The workflow ID.</param>
    /// <returns>A new <see cref="WorkflowExecutionError"/> instance.</returns>
    public static WorkflowExecutionError CompensationFailed(
        string stepName,
        string reason,
        string? workflowId = null)
    {
        return new WorkflowExecutionError($"Compensation failed for step '{stepName}': {reason}")
        {
            StepName = stepName,
            WorkflowId = workflowId,
            Code = "WF_COMPENSATION_FAILED",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.Critical
        };
    }

    /// <summary>
    ///     Creates an error for a cancelled workflow.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>A new <see cref="WorkflowExecutionError"/> instance.</returns>
    public static WorkflowExecutionError Cancelled(
        string workflowId,
        string? correlationId = null)
    {
        return new WorkflowExecutionError($"Workflow '{workflowId}' was cancelled")
        {
            WorkflowId = workflowId,
            CorrelationId = correlationId,
            Code = "WF_CANCELLED",
            Category = ErrorCategory.Cancelled,
            Severity = ErrorSeverity.Low
        };
    }

    #endregion
}
