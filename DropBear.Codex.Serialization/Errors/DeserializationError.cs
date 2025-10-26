#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Serialization.Errors;

/// <summary>
///     Represents an error that occurred during deserialization operations.
///     Use this instead of throwing DeserializationException.
/// </summary>
public sealed record DeserializationError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DeserializationError" /> class.
    /// </summary>
    /// <param name="message">The error message describing the deserialization failure.</param>
    public DeserializationError(string message) : base(message)
    {
    }

    /// <summary>
    ///     Gets or sets the target type for deserialization, if available.
    /// </summary>
    public Type? TargetType { get; init; }

    /// <summary>
    ///     Gets or sets the operation that failed.
    /// </summary>
    public string? Operation { get; init; }

    #region Factory Methods

    /// <summary>
    ///     Creates an error for a deserialization failure.
    /// </summary>
    /// <typeparam name="T">The type that failed to deserialize.</typeparam>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>A new DeserializationError instance.</returns>
    public static DeserializationError DeserializationFailed<T>(string reason)
    {
        return new DeserializationError($"Failed to deserialize to type '{typeof(T).Name}': {reason}")
        {
            TargetType = typeof(T),
            Operation = "Deserialize",
            Code = "DESER_FAILED",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for a deserialization failure without type information.
    /// </summary>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>A new DeserializationError instance.</returns>
    public static DeserializationError DeserializationFailed(string reason)
    {
        return new DeserializationError($"Deserialization failed: {reason}")
        {
            Operation = "Deserialize",
            Code = "DESER_FAILED",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for invalid or corrupted data.
    /// </summary>
    /// <param name="reason">The reason the data is invalid.</param>
    /// <returns>A new DeserializationError instance.</returns>
    public static DeserializationError InvalidData(string reason)
    {
        return new DeserializationError($"Invalid or corrupted data: {reason}")
        {
            Code = "DESER_INVALID_DATA",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for a type mismatch during deserialization.
    /// </summary>
    /// <typeparam name="TExpected">The expected type.</typeparam>
    /// <param name="actualType">The actual type name found in the data.</param>
    /// <returns>A new DeserializationError instance.</returns>
    public static DeserializationError TypeMismatch<TExpected>(string actualType)
    {
        return new DeserializationError($"Type mismatch: expected '{typeof(TExpected).Name}', found '{actualType}'")
        {
            TargetType = typeof(TExpected),
            Code = "DESER_TYPE_MISMATCH",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for null or empty input data.
    /// </summary>
    /// <returns>A new DeserializationError instance.</returns>
    public static DeserializationError NullOrEmptyData()
    {
        return new DeserializationError("Cannot deserialize null or empty data")
        {
            Code = "DESER_NULL_DATA",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.Medium
        };
    }

    /// <summary>
    ///     Creates an error for an unsupported format.
    /// </summary>
    /// <param name="format">The unsupported format.</param>
    /// <returns>A new DeserializationError instance.</returns>
    public static DeserializationError UnsupportedFormat(string format)
    {
        return new DeserializationError($"Unsupported format: {format}")
        {
            Code = "DESER_UNSUPPORTED_FORMAT",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.High
        };
    }

    #endregion
}
