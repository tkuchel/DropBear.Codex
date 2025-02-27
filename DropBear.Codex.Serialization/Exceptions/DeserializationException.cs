namespace DropBear.Codex.Serialization.Exceptions;

/// <summary>
///     Represents errors that occur during deserialization operations.
/// </summary>
public sealed class DeserializationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DeserializationException" /> class.
    /// </summary>
    public DeserializationException() : base("An error occurred during deserialization.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeserializationException" /> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DeserializationException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DeserializationException" /> class with a specified error message
    ///     and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DeserializationException(string message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    ///     Gets the type that failed to deserialize, if available.
    /// </summary>
    public Type? TargetType { get; init; }

    /// <summary>
    ///     Creates a new instance with information about the type that failed to deserialize.
    /// </summary>
    /// <typeparam name="T">The type that failed to deserialize.</typeparam>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    /// <returns>A new DeserializationException with type information.</returns>
    public static DeserializationException ForType<T>(string message, Exception? innerException = null)
    {
        return new DeserializationException(message, innerException) { TargetType = typeof(T) };
    }
}
