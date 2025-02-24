namespace DropBear.Codex.Core.Exceptions;

/// <summary>
///     Represents errors that occur during MessagePack serialization and deserialization.
/// </summary>
public class MessagePackSerializationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MessagePackSerializationException" /> class.
    /// </summary>
    public MessagePackSerializationException() : base(
        "An error occurred during MessagePack serialization or deserialization.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessagePackSerializationException" /> class with a specified error
    ///     message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public MessagePackSerializationException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessagePackSerializationException" /> class with a specified error
    ///     message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public MessagePackSerializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
