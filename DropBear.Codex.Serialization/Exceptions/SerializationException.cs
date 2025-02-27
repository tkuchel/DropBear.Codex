namespace DropBear.Codex.Serialization.Exceptions;

/// <summary>
///     Represents errors that occur during serialization operations.
/// </summary>
public sealed class SerializationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SerializationException" /> class.
    /// </summary>
    public SerializationException() : base("An error occurred during serialization.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SerializationException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SerializationException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SerializationException" /> class with a specified error message
    ///     and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public SerializationException(string message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    ///     Gets the type that failed to serialize, if available.
    /// </summary>
    public Type? SourceType { get; init; }

    /// <summary>
    ///     Creates a new instance with information about the type that failed to serialize.
    /// </summary>
    /// <typeparam name="T">The type that failed to serialize.</typeparam>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    /// <returns>A new SerializationException with type information.</returns>
    public static SerializationException ForType<T>(string message, Exception? innerException = null)
    {
        return new SerializationException(message, innerException) { SourceType = typeof(T) };
    }
}
