#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Workflow.Errors;

/// <summary>
///     Represents errors that occur when a workflow step times out.
///     Use this instead of throwing WorkflowStepTimeoutException.
/// </summary>
public sealed record WorkflowStepTimeoutError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="WorkflowStepTimeoutError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public WorkflowStepTimeoutError(string message) : base(message) { }

    /// <summary>
    ///     Gets or sets the name of the step that timed out.
    /// </summary>
    public string? StepName { get; init; }

    /// <summary>
    ///     Gets or sets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    ///     Gets or sets the actual duration the step ran before timing out.
    /// </summary>
    public TimeSpan? ActualDuration { get; init; }

    /// <summary>
    ///     Gets or sets the workflow ID.
    /// </summary>
    public string? WorkflowId { get; init; }

    #region Factory Methods

    /// <summary>
    ///     Creates an error for a step timeout.
    /// </summary>
    /// <param name="stepName">The name of the step that timed out.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="actualDuration">The actual duration the step ran.</param>
    /// <param name="workflowId">The workflow ID.</param>
    /// <returns>A new <see cref="WorkflowStepTimeoutError"/> instance.</returns>
    public static WorkflowStepTimeoutError StepTimedOut(
        string stepName,
        TimeSpan timeout,
        TimeSpan? actualDuration = null,
        string? workflowId = null)
    {
        var message = actualDuration.HasValue
            ? $"Step '{stepName}' timed out after {actualDuration.Value.TotalSeconds:F1}s (timeout: {timeout.TotalSeconds:F1}s)"
            : $"Step '{stepName}' timed out (timeout: {timeout.TotalSeconds:F1}s)";

        return new WorkflowStepTimeoutError(message)
        {
            StepName = stepName,
            Timeout = timeout,
            ActualDuration = actualDuration,
            WorkflowId = workflowId,
            Code = "WF_STEP_TIMEOUT",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for a workflow timeout.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="actualDuration">The actual duration the workflow ran.</param>
    /// <returns>A new <see cref="WorkflowStepTimeoutError"/> instance.</returns>
    public static WorkflowStepTimeoutError WorkflowTimedOut(
        string workflowId,
        TimeSpan timeout,
        TimeSpan? actualDuration = null)
    {
        var message = actualDuration.HasValue
            ? $"Workflow '{workflowId}' timed out after {actualDuration.Value.TotalSeconds:F1}s (timeout: {timeout.TotalSeconds:F1}s)"
            : $"Workflow '{workflowId}' timed out (timeout: {timeout.TotalSeconds:F1}s)";

        return new WorkflowStepTimeoutError(message)
        {
            WorkflowId = workflowId,
            Timeout = timeout,
            ActualDuration = actualDuration,
            Code = "WF_TIMEOUT",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.Critical
        };
    }

    #endregion
}
