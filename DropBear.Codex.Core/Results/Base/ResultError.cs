#region

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     An abstract record representing an error in a result-based operation.
///     Optimized for .NET 9+ with zero-allocation patterns and modern performance characteristics.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public abstract record ResultError : ISpanFormattable
{
    private const string DefaultErrorMessage = "An unknown error occurred";

    // Pre-defined common messages for zero-allocation scenarios
    private static readonly FrozenDictionary<string, string> CommonMessages =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["timeout"] = "The operation timed out",
            ["cancelled"] = "The operation was cancelled",
            ["unauthorized"] = "Access denied",
            ["notfound"] = "Resource not found",
            ["invalid"] = "Invalid input provided",
            ["network"] = "Network connection error",
            ["server"] = "Internal server error",
            ["validation"] = "Validation failed"
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ResultError" /> record.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    /// <param name="timestamp">Optional custom timestamp for the error. Defaults to UTC now.</param>
    protected ResultError(string message, DateTime? timestamp = null)
    {
        Message = string.IsNullOrWhiteSpace(message) ? DefaultErrorMessage : message.Trim();
        Timestamp = timestamp ?? DateTime.UtcNow;
        ErrorId = GenerateErrorId();
    }

    /// <summary>
    ///     Gets the error message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    ///     Gets the UTC timestamp when this error was created.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    ///     Gets the unique identifier for this error instance.
    /// </summary>
    public string ErrorId { get; init; }

    /// <summary>
    ///     Additional context or metadata associated with this error.
    ///     Uses FrozenDictionary for better read performance.
    /// </summary>
    [JsonExtensionData]
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    ///     Gets how long ago this error occurred relative to UTC now.
    /// </summary>
    [JsonIgnore]
    public TimeSpan Age => DateTime.UtcNow - Timestamp;

    /// <summary>
    ///     Gets the severity level of this error based on its age and type.
    /// </summary>
    [JsonIgnore]
    public ErrorSeverity Severity => DetermineSeverity();

    #region String Formatting

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    /// <inheritdoc />
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        var message = ToString();
        if (destination.Length < message.Length)
        {
            charsWritten = 0;
            return false;
        }

        message.AsSpan().CopyTo(destination);
        charsWritten = message.Length;
        return true;
    }

    /// <summary>
    ///     Creates an optimized string representation of this error.
    /// </summary>
    public override string ToString()
    {
        var typeName = GetType().Name;
        var ageText = FormatAge(Age);
        return $"{typeName}: {Message} (occurred {ageText} ago)";
    }

    /// <summary>
    ///     Creates a detailed string representation including metadata.
    /// </summary>
    public string ToDetailedString()
    {
        if (Metadata is null || Metadata.Count == 0)
        {
            return ToString();
        }

        var metadataString = string.Join(", ",
            Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        return $"{ToString()} [Metadata: {metadataString}]";
    }

    #endregion

    #region Error Modification Methods

    /// <summary>
    ///     Creates a new error with additional metadata.
    /// </summary>
    public virtual ResultError WithMetadata(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var newMetadata = Metadata switch
        {
            null => new Dictionary<string, object>(StringComparer.Ordinal) { [key] = value }
                .ToFrozenDictionary(StringComparer.Ordinal),
            _ => Metadata.ToDictionary(StringComparer.Ordinal)
                .Append(new KeyValuePair<string, object>(key, value))
                .ToFrozenDictionary(StringComparer.Ordinal)
        };

        return this with { Metadata = newMetadata };
    }

    /// <summary>
    ///     Creates a new error with multiple metadata entries.
    /// </summary>
    public virtual ResultError WithMetadata(IReadOnlyDictionary<string, object> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (metadata.Count == 0) return this;

        var combined = Metadata switch
        {
            null => metadata.ToFrozenDictionary(StringComparer.Ordinal),
            _ => Metadata.ToDictionary(StringComparer.Ordinal)
                .Concat(metadata)
                .ToFrozenDictionary(StringComparer.Ordinal)
        };

        return this with { Metadata = combined };
    }

    /// <summary>
    ///     Creates a new error with updated timestamp.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError WithTimestamp(DateTime newTimestamp)
    {
        return this with { Timestamp = newTimestamp };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Generates an optimized error ID for correlation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GenerateErrorId()
    {
        // Use Activity ID if available for better correlation
        if (Activity.Current?.Id is { } activityId)
        {
            return activityId;
        }

        // Fast unique ID generation without GUID overhead
        return $"{Environment.TickCount64:X}-{Random.Shared.Next():X}";
    }

    /// <summary>
    ///     Formats a TimeSpan age value into a human-readable string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatAge(TimeSpan age)
    {
        return age switch
        {
            { TotalMilliseconds: < 1000 } => $"{age.TotalMilliseconds:F0}ms",
            { TotalSeconds: < 60 } => $"{age.TotalSeconds:F1}s",
            { TotalMinutes: < 60 } => $"{age.TotalMinutes:F1}m",
            { TotalHours: < 24 } => $"{age.TotalHours:F1}h",
            _ => $"{age.TotalDays:F1}d"
        };
    }

    /// <summary>
    ///     Determines the severity of this error based on various factors.
    /// </summary>
    private ErrorSeverity DetermineSeverity()
    {
        var ageMinutes = Age.TotalMinutes;
        var messageLower = Message.ToLowerInvariant();

        return (ageMinutes, messageLower) switch
        {
            (< 1, _) when messageLower.Contains("critical") || messageLower.Contains("fatal")
                => ErrorSeverity.Critical,
            (< 5, _) when messageLower.Contains("error") || messageLower.Contains("failed")
                => ErrorSeverity.High,
            (< 15, _) => ErrorSeverity.Medium,
            (< 60, _) => ErrorSeverity.Low,
            _ => ErrorSeverity.Info
        };
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    ///     Creates a timeout error with standard messaging.
    /// </summary>
    public static TError CreateTimeout<TError>(TimeSpan timeoutDuration)
        where TError : ResultError
    {
        var message = $"Operation timed out after {FormatAge(timeoutDuration)}";
        return (TError)Activator.CreateInstance(typeof(TError), message)!;
    }

    /// <summary>
    ///     Creates a cancellation error with standard messaging.
    /// </summary>
    public static TError CreateCancellation<TError>()
        where TError : ResultError
    {
        return (TError)Activator.CreateInstance(typeof(TError), CommonMessages["cancelled"])!;
    }

    /// <summary>
    ///     Creates a validation error with field-specific messaging.
    /// </summary>
    public static TError CreateValidation<TError>(string fieldName, string reason)
        where TError : ResultError
    {
        var message = $"Validation failed for field '{fieldName}': {reason}";
        var error = (TError)Activator.CreateInstance(typeof(TError), message)!;

        return (TError)error
            .WithMetadata("FieldName", fieldName)
            .WithMetadata("ValidationReason", reason);
    }

    #endregion
}
