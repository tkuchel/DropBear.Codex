namespace DropBear.Codex.Utilities.Exceptions;

/// <summary>
/// Represents errors that occur during time-based code generation or validation.
/// </summary>
public sealed class TimeBasedCodeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeBasedCodeException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TimeBasedCodeException(string message, Exception innerException) : base(message, innerException) { }
}
