#region

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Base class for all result errors, providing common functionality.
///     Optimized for .NET 9 with modern C# features and frozen collections.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[DebuggerTypeProxy(typeof(ResultErrorDebugView))]
public abstract record ResultError
{
    private FrozenDictionary<string, object>? _metadata;

    /// <summary>
    ///     Initializes a new instance of ResultError with a required message.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected ResultError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
    }

    /// <summary>
    ///     Gets the error message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    ///     Gets or sets the severity of this error.
    ///     Default: Medium priority.
    /// </summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Medium;

    /// <summary>
    ///     Gets or sets the category of this error.
    ///     Default: General.
    /// </summary>
    public ErrorCategory Category { get; init; } = ErrorCategory.General;

    /// <summary>
    ///     Gets the error code, if any.
    ///     Useful for categorizing errors.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    ///     Gets the timestamp when this error was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     Gets a unique identifier for this error instance.
    /// </summary>
    public string ErrorId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     Gets the source exception if this error was created from an exception.
    ///     This property is useful for debugging and detailed error reporting while
    ///     maintaining separation between domain errors and technical exceptions.
    /// </summary>
    /// <remarks>
    ///     This property is marked with [JsonIgnore] to prevent serialization issues
    ///     with exceptions that may not be serializable. Stack trace information is
    ///     captured separately in the <see cref="StackTrace" /> property.
    /// </remarks>
    [JsonIgnore]
    public Exception? SourceException { get; init; }

    /// <summary>
    ///     Gets the stack trace captured when this error was created.
    ///     This is particularly useful when <see cref="SourceException" /> is not serialized.
    /// </summary>
    /// <remarks>
    ///     For errors created from exceptions, this will contain the exception's stack trace.
    ///     For manually created errors, this may be null or contain the creation call stack
    ///     if explicitly captured.
    /// </remarks>
    public string? StackTrace { get; init; }

    /// <summary>
    ///     Gets the inner exception message if the source exception has an inner exception.
    /// </summary>
    [JsonIgnore]
    public string? InnerExceptionMessage => SourceException?.InnerException?.Message;

    /// <summary>
    ///     Gets the age of this error (time since creation).
    /// </summary>
    public TimeSpan Age => DateTimeOffset.UtcNow - Timestamp;

    /// <summary>
    ///     Gets the metadata associated with this error.
    ///     Uses FrozenDictionary for optimal read performance.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, object> Metadata =>
        _metadata ?? FrozenDictionary<string, object>.Empty;

    /// <summary>
    ///     Gets whether this error has metadata.
    /// </summary>
    [JsonIgnore]
    public bool HasMetadata => _metadata?.Count > 0;

    /// <summary>
    ///     Gets whether this error has a source exception.
    /// </summary>
    [JsonIgnore]
    public bool HasException => SourceException != null;

    #region Debugger Support

    private string DebuggerDisplay =>
        $"{GetType().Name}: {Message}" +
        (Code != null ? $" [{Code}]" : "") +
        (HasException ? $" (Exception: {SourceException!.GetType().Name})" : "");

    #endregion

    #region Code Helpers

    /// <summary>
    ///     Creates a new error with the specified error code.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError WithCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return this with { Code = code };
    }

    /// <summary>
    ///     Creates a new error with the specified severity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError WithSeverity(ErrorSeverity severity) => this with { Severity = severity };

    /// <summary>
    ///     Creates a new error with the specified category.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError WithCategory(ErrorCategory category) => this with { Category = category };

    #endregion

    #region Metadata Management

    /// <summary>
    ///     Creates a new error with additional metadata.
    ///     This method is optimized for .NET 9 using frozen collections.
    /// </summary>
    public ResultError WithMetadata(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var newMetadata = _metadata != null
            ? new Dictionary<string, object>(_metadata, StringComparer.OrdinalIgnoreCase) { [key] = value }
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { [key] = value };

        return this with { _metadata = newMetadata.ToFrozenDictionary() };
    }

    /// <summary>
    ///     Creates a new error with multiple metadata entries.
    /// </summary>
    public ResultError WithMetadata(IEnumerable<KeyValuePair<string, object>> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var newMetadata = _metadata != null
            ? new Dictionary<string, object>(_metadata, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in metadata)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            newMetadata[key] = value;
        }

        return this with { _metadata = newMetadata.ToFrozenDictionary() };
    }

    /// <summary>
    ///     Attempts to get a metadata value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetMetadata<T>(string key, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_metadata?.TryGetValue(key, out var objValue) == true && objValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    #endregion

    #region Exception Helpers

    /// <summary>
    ///     Creates a new error with exception information.
    ///     This is typically used when converting an exception to a domain error.
    /// </summary>
    public ResultError WithException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return this with
        {
            SourceException = exception,
            StackTrace = exception.StackTrace ?? Environment.StackTrace,
            Message = string.IsNullOrWhiteSpace(Message) ? exception.Message : Message
        };
    }

    /// <summary>
    ///     Gets the full exception message including inner exceptions.
    /// </summary>
    public string GetFullExceptionMessage()
    {
        if (SourceException == null)
        {
            return Message;
        }

        var messages = new List<string> { Message };
        var current = SourceException;

        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                messages.Add(current.Message);
            }

            current = current.InnerException;
        }

        return string.Join(" -> ", messages.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    #endregion

    #region Formatting

    /// <summary>
    ///     Returns a string representation of this error.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(Code))
        {
            parts.Add($"[{Code}]");
        }

        parts.Add(Message);

        if (HasException)
        {
            parts.Add($"Exception: {SourceException!.GetType().Name}");
        }

        if (HasMetadata)
        {
            var metadataStr = string.Join(", ", _metadata!.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            parts.Add($"Metadata: [{metadataStr}]");
        }

        return string.Join(" | ", parts);
    }

    /// <summary>
    ///     Gets a detailed string representation including stack trace.
    /// </summary>
    public string ToDetailedString()
    {
        var builder = new StringBuilder();
        builder.AppendLine(ToString());

        if (!string.IsNullOrWhiteSpace(StackTrace))
        {
            builder.AppendLine("Stack Trace:");
            builder.AppendLine(StackTrace);
        }

        if (SourceException?.InnerException != null)
        {
            builder.AppendLine($"Inner Exception: {SourceException.InnerException.Message}");
        }

        return builder.ToString();
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///     Creates an error from an exception, preserving all exception information.
    /// </summary>
    public static TError FromException<TError>(Exception exception)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(exception);

        var error = new TError();
        return (TError)error.WithException(exception)
                .WithCode(exception.GetType().Name)
                .WithCategory(ErrorCategory.Technical)
                .WithSeverity(ErrorSeverity.High) with
            {
                Message = exception.Message
            };
    }

    /// <summary>
    ///     Creates an error from an exception with a custom message.
    /// </summary>
    public static TError FromException<TError>(Exception exception, string customMessage)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(customMessage);

        var error = new TError();
        return (TError)error.WithException(exception)
                .WithCode(exception.GetType().Name)
                .WithCategory(ErrorCategory.Technical)
                .WithSeverity(ErrorSeverity.High) with
            {
                Message = customMessage
            };
    }

    #endregion
}

