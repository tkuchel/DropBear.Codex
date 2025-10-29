namespace DropBear.Codex.Workflow.Exceptions;

/// <summary>
/// Exception thrown when workflow execution encounters an unrecoverable error.
/// </summary>
public sealed class WorkflowExecutionException : Exception
{
    /// <summary>
    /// Gets the workflow ID that failed.
    /// </summary>
    public string? WorkflowId { get; }

    /// <summary>
    /// Gets the correlation ID for the failed execution.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// Initializes a new workflow execution exception.
    /// </summary>
    public WorkflowExecutionException()
    {
    }

    /// <summary>
    /// Initializes a new workflow execution exception with a message.
    /// </summary>
    /// <param name="message">Error message</param>
    public WorkflowExecutionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new workflow execution exception with a message and inner exception.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="innerException">Inner exception that caused the failure</param>
    public WorkflowExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new workflow execution exception.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="workflowId">ID of the workflow that failed</param>
    /// <param name="correlationId">Correlation ID for the execution</param>
    /// <param name="innerException">Inner exception that caused the failure</param>
    public WorkflowExecutionException(
        string message,
        string? workflowId = null,
        string? correlationId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        WorkflowId = workflowId;
        CorrelationId = correlationId;
    }
}
