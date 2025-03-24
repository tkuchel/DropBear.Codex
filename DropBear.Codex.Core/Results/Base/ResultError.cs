#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     An abstract record representing an error in a result-based operation.
///     This forms the base type for all domain-specific errors in the Result pattern.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public abstract record ResultError : ISpanFormattable
{
    private const string DefaultErrorMessage = "An unknown error occurred";

    // Cache frequently-used error messages to reduce string allocations
    private static readonly ConcurrentDictionary<string, string> MessageCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="ResultError" /> record.
    /// </summary>
    /// <param name="message">The error message describing the failure condition.</param>
    /// <param name="timestamp">Optional custom timestamp for the error. Defaults to UTC now.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="message" /> is null or whitespace.</exception>
    protected ResultError(string message, DateTime? timestamp = null)
    {
        Message = ValidateAndFormatMessage(message);
        Timestamp = timestamp ?? DateTime.UtcNow;
        ErrorId = Activity.Current?.Id ?? string.Empty;
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
    ///     Gets the unique identifier for this error instance, correlating with telemetry.
    /// </summary>
    public string ErrorId { get; init; }

    /// <summary>
    ///     Additional context or metadata associated with this error.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? Metadata { get; protected internal init; }

    /// <summary>
    ///     Indicates whether this error is a default/unknown error (i.e., no message provided).
    /// </summary>
    [JsonIgnore]
    public bool IsDefaultError => Message.Equals(DefaultErrorMessage, StringComparison.Ordinal);

    /// <summary>
    ///     Gets how long ago (relative to now) this error occurred.
    /// </summary>
    [JsonIgnore]
    public TimeSpan Age => DateTime.UtcNow - Timestamp;

    #region String Formatting

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return ToString();
    }

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
    ///     Creates a string representation of this error, including how long ago it occurred.
    /// </summary>
    public override string ToString()
    {
        return $"{GetType().Name}: {Message} (occurred {FormatAge(Age)} ago)";
    }

    #endregion

    #region Error Modification Methods

    /// <summary>
    ///     Creates a new error object with an updated message.
    /// </summary>
    /// <param name="newMessage">The new error message.</param>
    /// <returns>A new <see cref="ResultError" /> instance with the updated message.</returns>
    protected ResultError WithUpdatedMessage(string newMessage)
    {
        return this with { Message = ValidateAndFormatMessage(newMessage) };
    }


    #endregion

    #region Helper Methods

    /// <summary>
    ///     Validates and formats an error message, using a cached version if available.
    /// </summary>
    /// <param name="message">The message to validate and format.</param>
    /// <returns>The formatted message.</returns>
    private static string ValidateAndFormatMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return DefaultErrorMessage;
        }

        // Use our cache to avoid repeated string allocations for same messages
        return MessageCache.GetOrAdd(message, key =>
        {
            // Replace CRLF with \n
            var normalized = key.Replace("\r\n", "\n").Replace('\r', '\n');
            return normalized.Trim();
        });
    }

    /// <summary>
    ///     Formats a TimeSpan age value into a human-readable string.
    /// </summary>
    /// <param name="age">The age to format.</param>
    /// <returns>A formatted string representing the age.</returns>
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

    #endregion
}
