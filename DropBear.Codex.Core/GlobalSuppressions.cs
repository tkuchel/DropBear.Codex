#region

using System.Diagnostics.CodeAnalysis;

#endregion

// =====================================================================
// SERIALIZATION DTOs - Dictionary Required for MessagePack Compatibility
// =====================================================================

[assembly: SuppressMessage(
    "Design",
    "CA1002:Do not expose generic lists",
    Justification = "Dictionary<string, object> is required for MessagePack binary serialization compatibility. " +
                    "This DTO is an internal serialization contract that converts to FrozenDictionary<string, object> " +
                    "in the domain model (Envelope<T>) for optimal read performance. Using IReadOnlyDictionary or " +
                    "IDictionary would break MessagePack serialization without custom formatters.",
    Scope = "member",
    Target = "~P:DropBear.Codex.Core.Envelopes.EnvelopeDto`1.Headers")]

// =====================================================================
// EXTENSION METHODS - Collection Return Types for Performance
// =====================================================================

// WhereAsync returns List<T> for optimal performance in async LINQ scenarios
[assembly: SuppressMessage(
    "Design",
    "CA1002:Do not expose generic lists",
    Justification = "List<T> provides better performance for materialized async LINQ operations. " +
                    "Extension method consumers can easily convert to IReadOnlyList<T> or IEnumerable<T> " +
                    "if needed using standard LINQ methods. Returning an interface would add unnecessary " +
                    "allocation overhead for the common case where consumers need the concrete type for " +
                    "further operations. This is a standard pattern in LINQ extension methods.",
    Scope = "member",
    Target =
        "~M:DropBear.Codex.Core.Extensions.ConcurrentDictionaryExtensions.WhereAsync``2(System.Collections.Concurrent.ConcurrentDictionary{``0,``1},System.Func{``0,``1,System.Threading.Tasks.ValueTask{System.Boolean}},System.Threading.CancellationToken)~System.Threading.Tasks.ValueTask{System.Collections.Generic.List{``1}}")]

// SelectAsync returns Dictionary<TKey, TResult> for optimal performance in async transformations
[assembly: SuppressMessage(
    "Design",
    "CA1002:Do not expose generic lists",
    Justification = "Dictionary<TKey, TResult> provides better performance for materialized async transformations. " +
                    "Extension method consumers can easily convert to IReadOnlyDictionary<TKey, TResult> or " +
                    "IDictionary<TKey, TResult> if needed. Returning an interface would add unnecessary " +
                    "allocation overhead for the common case where consumers need the concrete type for " +
                    "lookups, modifications, or serialization. Pre-sizing with dictionary.Count optimizes allocation.",
    Scope = "member",
    Target =
        "~M:DropBear.Codex.Core.Extensions.ConcurrentDictionaryExtensions.SelectAsync``3(System.Collections.Concurrent.ConcurrentDictionary{``0,``1},System.Func{``0,``1,System.Threading.Tasks.ValueTask{``2}},System.Threading.CancellationToken)~System.Threading.Tasks.ValueTask{System.Collections.Generic.Dictionary{``0,``2}}")]
// =====================================================================
// DIAGNOSTIC INFO - Uses IReadOnlyDictionary for structured logging
// =====================================================================
// DiagnosticInfo.ToDictionary() returns IReadOnlyDictionary<string, object>
// following CA1002 best practices. This is appropriate for diagnostic
// data that should not be modified by consumers.
