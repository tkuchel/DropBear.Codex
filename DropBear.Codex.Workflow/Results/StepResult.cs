namespace DropBear.Codex.Workflow.Results;

/// <summary>
/// Represents the result of a single step execution.
/// Uses discriminated union pattern for type-safe error handling.
/// </summary>
public readonly record struct StepResult
{
    /// <summary>
    /// Indicates whether the step executed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message when step execution fails.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Additional metadata about the step execution.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Indicates whether this step should be retried on failure.
    /// </summary>
    public bool ShouldRetry { get; init; }

    /// <summary>
    /// Creates a successful step result.
    /// </summary>
    /// <param name="metadata">Optional metadata to include</param>
    /// <returns>A successful step result</returns>
    public static StepResult Success(IReadOnlyDictionary<string, object>? metadata = null) =>
        new() { IsSuccess = true, Metadata = metadata };

    /// <summary>
    /// Creates a failed step result with an error message.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="shouldRetry">Whether this step should be retried</param>
    /// <param name="metadata">Optional metadata to include</param>
    /// <returns>A failed step result</returns>
    public static StepResult Failure(string message, bool shouldRetry = false, IReadOnlyDictionary<string, object>? metadata = null) =>
        new() 
        { 
            IsSuccess = false, 
            ErrorMessage = message, 
            ShouldRetry = shouldRetry,
            Metadata = metadata 
        };

    /// <summary>
    /// Creates a failed step result with an exception.
    /// </summary>
    /// <param name="exception">The exception that caused the failure</param>
    /// <param name="shouldRetry">Whether this step should be retried</param>
    /// <param name="metadata">Optional metadata to include</param>
    /// <returns>A failed step result</returns>
    public static StepResult Failure(Exception exception, bool shouldRetry = false, IReadOnlyDictionary<string, object>? metadata = null) =>
        new() 
        { 
            IsSuccess = false, 
            ErrorMessage = exception.Message, 
            Exception = exception,
            ShouldRetry = shouldRetry,
            Metadata = metadata 
        };
}
