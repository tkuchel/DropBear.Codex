namespace DropBear.Codex.Utilities.Exceptions;

/// <summary>
///     Represents errors that occur during password jumbling or unjumbling operations.
/// </summary>
public sealed class JumblerException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="JumblerException" /> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public JumblerException(string message, Exception innerException) : base(message, innerException) { }
}
