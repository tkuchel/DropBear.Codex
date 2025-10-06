namespace DropBear.Codex.Workflow.Exceptions;

/// <summary>
/// Exception thrown when workflow configuration is invalid.
/// </summary>
public sealed class WorkflowConfigurationException : Exception
{
    /// <summary>
    /// Gets the workflow ID with invalid configuration.
    /// </summary>
    public string? WorkflowId { get; }

    /// <summary>
    /// Initializes a new workflow configuration exception.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="workflowId">ID of the workflow with invalid configuration</param>
    /// <param name="innerException">Inner exception that caused the failure</param>
    public WorkflowConfigurationException(
        string message,
        string? workflowId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        WorkflowId = workflowId;
    }
}
