#region

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    #endregion

    #region Metadata Management

    /// <summary>
    ///     Adds a single metadata entry to this error.
    ///     Uses modern 'with' expression for immutability.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new error instance with the metadata added.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError WithMetadata(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var builder = _metadata?.ToDictionary(StringComparer.Ordinal)
                      ?? new Dictionary<string, object>(StringComparer.Ordinal);

        builder[key] = value;

        return this with { _metadata = builder.ToFrozenDictionary(StringComparer.Ordinal) };
    }

    /// <summary>
    ///     Adds multiple metadata entries to this error.
    ///     Uses collection expressions for modern syntax.
    /// </summary>
    /// <param name="items">The metadata items to add.</param>
    /// <returns>A new error instance with the metadata added.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError WithMetadata(IReadOnlyDictionary<string, object> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return this;
        }

        var builder = _metadata?.ToDictionary(StringComparer.Ordinal)
                      ?? new Dictionary<string, object>(items.Count, StringComparer.Ordinal);

        foreach (var (key, value) in items)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            builder[key] = value;
        }

        return this with { _metadata = builder.ToFrozenDictionary(StringComparer.Ordinal) };
    }

    /// <summary>
    ///     Gets a metadata value by key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T? GetMetadata<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_metadata?.TryGetValue(key, out var value) == true && value is T typed)
        {
            return typed;
        }

        return default;
    }

    /// <summary>
    ///     Tries to get a metadata value by key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetMetadata<T>(string key, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_metadata?.TryGetValue(key, out var obj) == true && obj is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    #endregion

    #region Severity Helpers

    /// <summary>
    ///     Creates a new error with the specified severity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError WithSeverity(ErrorSeverity severity) => this with { Severity = severity };

    /// <summary>
    ///     Creates a new error with Info severity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError AsInfo() => this with { Severity = ErrorSeverity.Info };

    /// <summary>
    ///     Creates a new error with Low severity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError AsLow() => this with { Severity = ErrorSeverity.Low };

    /// <summary>
    ///     Creates a new error with Medium severity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError AsMedium() => this with { Severity = ErrorSeverity.Medium };

    /// <summary>
    ///     Creates a new error with High severity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError AsHigh() => this with { Severity = ErrorSeverity.High };

    /// <summary>
    ///     Creates a new error with Critical severity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ResultError AsCritical() => this with { Severity = ErrorSeverity.Critical };

    #endregion

    #region Display

    private string DebuggerDisplay => $"[{Severity}] {Code ?? "NO_CODE"}: {Message}";

    /// <summary>
    ///     Returns a string representation of this error.
    /// </summary>
    public override string ToString()
    {
        return Code is not null
            ? $"[{Code}] {Message}"
            : Message;
    }

    #endregion

    #region Performance-Optimized Metadata

    /// <summary>
    ///     Optimized metadata initialization using frozen dictionary builder.
    ///     Reduces allocations for metadata-heavy scenarios.
    ///     NOTE: This is a helper method - concrete error types should use their own factory methods.
    /// </summary>
    /// <typeparam name="TError">The concrete error type to create.</typeparam>
    public static TError CreateWithMetadata<TError>(
        string message,
        ReadOnlySpan<KeyValuePair<string, object>> metadata)
        where TError : ResultError
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        // Create the concrete error instance
        var error = (TError)Activator.CreateInstance(typeof(TError), message)!;

        if (metadata.IsEmpty)
        {
            return error;
        }

        // Use frozen dictionary builder for optimal performance
        var builder = metadata.Length <= 4
            ? new Dictionary<string, object>(metadata.Length, StringComparer.Ordinal)
            : new Dictionary<string, object>(StringComparer.Ordinal);

        for (var i = 0; i < metadata.Length; i++)
        {
            ref readonly var kvp = ref metadata[i];
            builder[kvp.Key] = kvp.Value;
        }

        return error with { _metadata = builder.ToFrozenDictionary(StringComparer.Ordinal) };
    }

    /// <summary>
    ///     Fast metadata lookup using TryGetValue with aggressive inlining.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetMetadataFast<T>(ReadOnlySpan<char> key, out T? value)
    {
        // Convert span to string only if metadata exists
        if (_metadata is null || _metadata.Count == 0)
        {
            value = default;
            return false;
        }

        var keyStr = key.ToString();
        if (_metadata.TryGetValue(keyStr, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    #endregion
}
