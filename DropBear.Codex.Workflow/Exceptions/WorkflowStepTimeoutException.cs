namespace DropBear.Codex.Workflow.Exceptions;

/// <summary>
/// Exception thrown when a workflow step times out.
/// </summary>
public sealed class WorkflowStepTimeoutException : Exception
{
    /// <summary>
    /// Gets the name of the step that timed out.
    /// </summary>
    public string? StepName { get; }

    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan? Timeout { get; }

    /// <summary>
    /// Initializes a new workflow step timeout exception.
    /// </summary>
    public WorkflowStepTimeoutException()
    {
    }

    /// <summary>
    /// Initializes a new workflow step timeout exception with a message.
    /// </summary>
    /// <param name="message">Error message</param>
    public WorkflowStepTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new workflow step timeout exception with a message and inner exception.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="innerException">Inner exception that caused the failure</param>
    public WorkflowStepTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new workflow step timeout exception.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="stepName">Name of the step that timed out</param>
    /// <param name="timeout">Timeout duration that was exceeded</param>
    /// <param name="innerException">Inner exception that caused the failure</param>
    public WorkflowStepTimeoutException(
        string message,
        string? stepName = null,
        TimeSpan? timeout = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StepName = stepName;
        Timeout = timeout;
    }
}
