namespace DropBear.Codex.Serialization.Exceptions;

/// <summary>
///     Represents errors that occur during compression or decompression operations.
///     OBSOLETE: Use Result&lt;T, CompressionError&gt; pattern instead.
/// </summary>
[Obsolete("Use Result<T, CompressionError> pattern instead of throwing exceptions. See DropBear.Codex.Serialization.Errors.CompressionError", false)]
public sealed class CompressionException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CompressionException" /> class.
    /// </summary>
    public CompressionException() : base("An error occurred during compression or decompression.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="CompressionException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CompressionException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="CompressionException" /> class with a specified error message
    ///     and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CompressionException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}
