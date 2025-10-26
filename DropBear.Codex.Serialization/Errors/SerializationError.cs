#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Serialization.Errors;

/// <summary>
///     Represents an error that occurred during serialization operations.
///     Use this instead of throwing SerializationException.
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
    public string? Operation { get; init; }

    #region Factory Methods

    /// <summary>
    ///     Creates an error for a serialization failure.
    /// </summary>
    /// <typeparam name="T">The type that failed to serialize.</typeparam>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>A new SerializationError instance.</returns>
    public static SerializationError SerializationFailed<T>(string reason)
    {
        return new SerializationError($"Failed to serialize type '{typeof(T).Name}': {reason}")
        {
            OperationType = typeof(T),
            Operation = "Serialize",
            Code = "SER_SERIALIZE_FAILED",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for a serialization failure without type information.
    /// </summary>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>A new SerializationError instance.</returns>
    public static SerializationError SerializationFailed(string reason)
    {
        return new SerializationError($"Serialization failed: {reason}")
        {
            Operation = "Serialize",
            Code = "SER_SERIALIZE_FAILED",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for invalid input data.
    /// </summary>
    /// <param name="reason">The reason the input is invalid.</param>
    /// <returns>A new SerializationError instance.</returns>
    public static SerializationError InvalidInput(string reason)
    {
        return new SerializationError($"Invalid input data: {reason}")
        {
            Code = "SER_INVALID_INPUT",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.Medium
        };
    }

    /// <summary>
    ///     Creates an error for a null value.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that was null.</param>
    /// <returns>A new SerializationError instance.</returns>
    public static SerializationError NullValue(string parameterName)
    {
        return new SerializationError($"Value cannot be null: {parameterName}")
        {
            Code = "SER_NULL_VALUE",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.Medium
        };
    }

    /// <summary>
    ///     Creates an error for an unsupported type.
    /// </summary>
    /// <typeparam name="T">The unsupported type.</typeparam>
    /// <returns>A new SerializationError instance.</returns>
    public static SerializationError UnsupportedType<T>()
    {
        return new SerializationError($"Type '{typeof(T).Name}' is not supported for serialization")
        {
            OperationType = typeof(T),
            Code = "SER_UNSUPPORTED_TYPE",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.High
        };
    }

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

    #endregion
}
