#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents an error that occurred during JavaScript interop operations.
///     Enhanced for .NET 9 with improved error context and performance.
/// </summary>
public sealed class JsInteropError : IEquatable<JsInteropError>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="JsInteropError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    /// <param name="operationName">The name of the operation that failed.</param>
    /// <param name="caller">The calling method name.</param>
    public JsInteropError(
        string message,
        Exception? innerException = null,
        string? operationName = null,
        [CallerMemberName] string? caller = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        InnerException = innerException;
        OperationName = operationName;
        Caller = caller;
        Timestamp = DateTimeOffset.UtcNow;
        ErrorId = Guid.NewGuid();
    }

    /// <summary>
    ///     Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets the inner exception that caused this error.
    /// </summary>
    public Exception? InnerException { get; }

    /// <summary>
    ///     Gets the name of the operation that failed.
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    ///     Gets the calling method name.
    /// </summary>
    public string? Caller { get; }

    /// <summary>
    ///     Gets the timestamp when the error occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    ///     Gets a unique identifier for this error instance.
    /// </summary>
    public Guid ErrorId { get; }

    /// <summary>
    ///     Gets the full error context including stack trace information.
    /// </summary>
    public string FullContext =>
        $"[{ErrorId}] {Message}" +
        (OperationName != null ? $" (Operation: {OperationName})" : "") +
        (Caller != null ? $" (Caller: {Caller})" : "") +
        $" at {Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC" +
        (InnerException != null ? $"\nInner Exception: {InnerException}" : "");

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    public bool Equals(JsInteropError? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return ErrorId.Equals(other.ErrorId) &&
               string.Equals(Message, other.Message, StringComparison.Ordinal) &&
               string.Equals(OperationName, other.OperationName, StringComparison.Ordinal) &&
               string.Equals(Caller, other.Caller, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    public override bool Equals(object? obj) => Equals(obj as JsInteropError);

    /// <summary>
    ///     Returns a hash code for the current object.
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(ErrorId, Message, OperationName, Caller);

    /// <summary>
    ///     Returns a string representation of the error.
    /// </summary>
    public override string ToString() => FullContext;

    /// <summary>
    ///     Equality operator.
    /// </summary>
    public static bool operator ==(JsInteropError? left, JsInteropError? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>
    ///     Inequality operator.
    /// </summary>
    public static bool operator !=(JsInteropError? left, JsInteropError? right) => !(left == right);
}
