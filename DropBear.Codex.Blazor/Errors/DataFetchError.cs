#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Extensions;

#endregion

namespace DropBear.Codex.Blazor.Errors;

/// <summary>
///     Represents an error that occurred during data fetch operations.
///     Enhanced for .NET 9 with improved error context and performance.
/// </summary>
public sealed class DataFetchError : IEquatable<DataFetchError>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DataFetchError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="operationName">The name of the operation that failed.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    /// <param name="caller">The calling method name.</param>
    public DataFetchError(
        string message,
        string? operationName = null,
        Exception? innerException = null,
        [CallerMemberName] string? caller = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        OperationName = operationName;
        InnerException = innerException;
        Caller = caller;
        Timestamp = DateTimeOffset.UtcNow;
        ErrorId = Guid.NewGuid();
    }

    /// <summary>
    ///     Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets the name of the operation that failed.
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    ///     Gets the inner exception that caused this error.
    /// </summary>
    public Exception? InnerException { get; }

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
    ///     Gets the operation type for categorization.
    /// </summary>
    public DataFetchErrorType ErrorType { get; init; } = DataFetchErrorType.Unknown;

    /// <summary>
    ///     Gets additional context data for the error.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    /// <summary>
    ///     Gets whether this error is retryable.
    /// </summary>
    public bool IsRetryable => ErrorType switch
    {
        DataFetchErrorType.Timeout => true,
        DataFetchErrorType.NetworkError => true,
        DataFetchErrorType.TemporaryServiceUnavailable => true,
        DataFetchErrorType.RateLimited => true,
        _ => false
    };

    /// <summary>
    ///     Gets the full error context including all available information.
    /// </summary>
    public string FullContext =>
        $"[{ErrorId}] {Message}" +
        (OperationName != null ? $" (Operation: {OperationName})" : "") +
        (Caller != null ? $" (Caller: {Caller})" : "") +
        $" (Type: {ErrorType})" +
        $" (Retryable: {IsRetryable})" +
        $" at {Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC" +
        (Context?.Count > 0 ? $"\nContext: {string.Join(", ", Context.Select(kvp => $"{kvp.Key}={kvp.Value}"))}" : "") +
        (InnerException != null ? $"\nInner Exception: {InnerException}" : "");

    /// <summary>
    ///     Creates a new DataFetchError with additional context.
    /// </summary>
    public DataFetchError WithContext(string key, object value)
    {
        var newContext = Context?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>();
        newContext[key] = value;

        return new DataFetchError(Message, OperationName, InnerException, Caller)
        {
            ErrorType = ErrorType, Context = newContext.AsReadOnly()
        };
    }

    /// <summary>
    ///     Creates a new DataFetchError with a specific error type.
    /// </summary>
    public DataFetchError WithErrorType(DataFetchErrorType errorType)
    {
        return new DataFetchError(Message, OperationName, InnerException, Caller)
        {
            ErrorType = errorType, Context = Context
        };
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    public bool Equals(DataFetchError? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return ErrorId.Equals(other.ErrorId) &&
               string.Equals(Message, other.Message, StringComparison.Ordinal) &&
               string.Equals(OperationName, other.OperationName, StringComparison.Ordinal) &&
               ErrorType == other.ErrorType;
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    public override bool Equals(object? obj) => Equals(obj as DataFetchError);

    /// <summary>
    ///     Returns a hash code for the current object.
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(ErrorId, Message, OperationName, ErrorType);

    /// <summary>
    ///     Returns a string representation of the error.
    /// </summary>
    public override string ToString() => FullContext;

    /// <summary>
    ///     Equality operator.
    /// </summary>
    public static bool operator ==(DataFetchError? left, DataFetchError? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>
    ///     Inequality operator.
    /// </summary>
    public static bool operator !=(DataFetchError? left, DataFetchError? right) => !(left == right);
}
