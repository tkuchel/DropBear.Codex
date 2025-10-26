#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Workflow.Errors;

/// <summary>
///     Represents errors that occur during workflow configuration.
///     Use this instead of throwing WorkflowConfigurationException.
/// </summary>
public sealed record WorkflowConfigurationError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="WorkflowConfigurationError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public WorkflowConfigurationError(string message) : base(message) { }

    /// <summary>
    ///     Gets or sets the workflow ID with invalid configuration.
    /// </summary>
    public string? WorkflowId { get; init; }

    /// <summary>
    ///     Gets or sets the property name that has an invalid configuration.
    /// </summary>
    public string? PropertyName { get; init; }

    #region Factory Methods

    /// <summary>
    ///     Creates an error for invalid workflow configuration.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="reason">The reason for the invalid configuration.</param>
    /// <returns>A new <see cref="WorkflowConfigurationError"/> instance.</returns>
    public static WorkflowConfigurationError InvalidConfiguration(
        string workflowId,
        string reason)
    {
        return new WorkflowConfigurationError($"Invalid configuration for workflow '{workflowId}': {reason}")
        {
            WorkflowId = workflowId,
            Code = "WF_INVALID_CONFIG",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for a missing required workflow property.
    /// </summary>
    /// <param name="propertyName">The name of the missing property.</param>
    /// <param name="workflowId">The workflow ID.</param>
    /// <returns>A new <see cref="WorkflowConfigurationError"/> instance.</returns>
    public static WorkflowConfigurationError MissingRequired(
        string propertyName,
        string? workflowId = null)
    {
        return new WorkflowConfigurationError($"Required property '{propertyName}' is missing")
        {
            PropertyName = propertyName,
            WorkflowId = workflowId,
            Code = "WF_MISSING_REQUIRED",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for an invalid workflow step configuration.
    /// </summary>
    /// <param name="stepName">The name of the step with invalid configuration.</param>
    /// <param name="reason">The reason for the invalid configuration.</param>
    /// <param name="workflowId">The workflow ID.</param>
    /// <returns>A new <see cref="WorkflowConfigurationError"/> instance.</returns>
    public static WorkflowConfigurationError InvalidStep(
        string stepName,
        string reason,
        string? workflowId = null)
    {
        return new WorkflowConfigurationError($"Invalid step '{stepName}' configuration: {reason}")
        {
            PropertyName = stepName,
            WorkflowId = workflowId,
            Code = "WF_INVALID_STEP",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for a circular dependency in the workflow.
    /// </summary>
    /// <param name="workflowId">The workflow ID.</param>
    /// <param name="details">Details about the circular dependency.</param>
    /// <returns>A new <see cref="WorkflowConfigurationError"/> instance.</returns>
    public static WorkflowConfigurationError CircularDependency(
        string workflowId,
        string details)
    {
        return new WorkflowConfigurationError($"Circular dependency detected in workflow '{workflowId}': {details}")
        {
            WorkflowId = workflowId,
            Code = "WF_CIRCULAR_DEPENDENCY",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.Critical
        };
    }

    /// <summary>
    ///     Creates an error for an invalid workflow builder state.
    /// </summary>
    /// <param name="reason">The reason for the invalid state.</param>
    /// <returns>A new <see cref="WorkflowConfigurationError"/> instance.</returns>
    public static WorkflowConfigurationError InvalidBuilderState(string reason)
    {
        return new WorkflowConfigurationError($"Invalid workflow builder state: {reason}")
        {
            Code = "WF_INVALID_BUILDER",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.High
        };
    }

    #endregion
}
