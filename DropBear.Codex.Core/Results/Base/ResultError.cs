#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     An abstract record representing an error in a result-based operation.
///     Derived types can provide domain-specific error information.
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
    public string Message { get; private init; }

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
    public Dictionary<string, object>? Metadata { get; protected init; }

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

    /// <summary>
    ///     Creates a new error object with an updated message.
    /// </summary>
    /// <param name="newMessage">The new error message.</param>
    /// <returns>A new <see cref="ResultError" /> instance with the updated message.</returns>
    protected ResultError WithUpdatedMessage(string newMessage)
    {
        return this with { Message = ValidateAndFormatMessage(newMessage) };
    }

    /// <summary>
    ///     Adds metadata to the error context.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new instance with updated metadata.</returns>
    public ResultError WithMetadata(string key, object value)
    {
        var metadata = Metadata is null
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(Metadata, StringComparer.Ordinal);

        metadata[key] = value;
        return this with { Metadata = metadata };
    }

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

        // Normalize and cache the message
        return MessageCache.GetOrAdd(message, key =>
        {
            var span = key.AsSpan();

            // Count how many \r\n sequences to determine final length
            var crlfCount = 0;
            for (var i = 0; i < span.Length - 1; i++)
            {
                if (span[i] == '\r' && span[i + 1] == '\n')
                {
                    crlfCount++;
                }
            }

            // Create the normalized string
            return string.Create(key.Length - crlfCount, key, (dest, src) =>
            {
                var destIndex = 0;
                var sourceSpan = src.AsSpan();

                for (var i = 0; i < sourceSpan.Length; i++)
                {
                    var c = sourceSpan[i];

                    if (c == '\r')
                    {
                        // Skip \r and replace with \n, skip following \n if present
                        if (i + 1 < sourceSpan.Length && sourceSpan[i + 1] == '\n')
                        {
                            i++; // Skip the following \n
                        }

                        dest[destIndex++] = '\n';
                    }
                    else
                    {
                        dest[destIndex++] = c;
                    }
                }
            }).Trim();
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

    /// <summary>
    ///     Creates a new error with the current timestamp.
    /// </summary>
    /// <typeparam name="T">The specific error type to create.</typeparam>
    /// <param name="message">The error message.</param>
    /// <returns>A new error instance.</returns>
    protected static T CreateNow<T>(string message) where T : ResultError
    {
        return (T)Activator.CreateInstance(typeof(T), message, DateTime.UtcNow)!;
    }

    /// <summary>
    ///     Extension point for derived types to customize error creation.
    /// </summary>
    protected virtual void OnErrorCreated()
    {
        // Default implementation does nothing
    }
}
