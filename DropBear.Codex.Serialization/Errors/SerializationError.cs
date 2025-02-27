#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Serialization.Errors;

/// <summary>
///     Represents an error that occurred during serialization operations.
/// </summary>
public sealed record SerializationError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SerializationError" /> class.
    /// </summary>
    /// <param name="message">The error message describing the serialization failure.</param>
    public SerializationError(string message) : base(message)
    {
    }

    /// <summary>
    ///     Gets or sets the type involved in the serialization operation, if available.
    /// </summary>
    public Type? OperationType { get; init; }

    /// <summary>
    ///     Gets or sets the operation that failed (e.g., "Serialize", "Deserialize", "Compress").
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    ///     Creates a new error with information about the operation type.
    /// </summary>
    /// <typeparam name="T">The type involved in the serialization operation.</typeparam>
    /// <param name="message">The error message.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <returns>A new SerializationError with type information.</returns>
    public static SerializationError ForType<T>(string message, string operation)
    {
        return new SerializationError(message) { OperationType = typeof(T), Operation = operation };
    }

    /// <summary>
    ///     Creates a new error with the specified operation.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <returns>A new SerializationError with the operation information.</returns>
    public SerializationError WithOperation(string operation)
    {
        return this with { Operation = operation };
    }
}
