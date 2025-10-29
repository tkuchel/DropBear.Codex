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

// =====================================================================
// RESULT PATTERN - Static Factory Methods on Generic Types
// =====================================================================
// CA1000: Static factory methods on generic Result types are intentional
// for fluent API design and are a core pattern of Railway-Oriented Programming.

[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Static factory methods on generic Result types are intentional for fluent API design and are a core pattern of Railway-Oriented Programming.",
    Scope = "namespaceanddescendants",
    Target = "~N:DropBear.Codex.Core.Results")]

[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Static factory methods on generic Envelope is intentional for creating instances.",
    Scope = "type",
    Target = "~T:DropBear.Codex.Core.Envelopes.Envelope`1")]

[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Static factory method for generic CompositeEnvelope is intentional.",
    Scope = "type",
    Target = "~T:DropBear.Codex.Core.Envelopes.CompositeEnvelope`1")]

// =====================================================================
// RESULT PATTERN - Exception Handling
// =====================================================================
// CA1031: Result pattern intentionally catches all exceptions to wrap them
// in Result types for Railway-Oriented Programming. This is safer than
// letting exceptions bubble up.

[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types",
    Justification = "Result pattern intentionally catches all exceptions to wrap them in Result types for Railway-Oriented Programming. This is safer than letting exceptions bubble up.",
    Scope = "namespaceanddescendants",
    Target = "~N:DropBear.Codex.Core.Results")]

[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types",
    Justification = "Extension methods for Result pattern need to catch all exceptions to maintain monadic behavior.",
    Scope = "type",
    Target = "~T:DropBear.Codex.Core.Extensions.UnitExtensions")]

// =====================================================================
// API DESIGN - Methods vs Properties
// =====================================================================

[assembly: SuppressMessage("Design", "CA1024:Use properties where appropriate",
    Justification = "Methods return new instances each time and have side effects, so they should not be properties.",
    Scope = "type",
    Target = "~T:DropBear.Codex.Core.Extensions.MessagePackConfig")]

[assembly: SuppressMessage("Design", "CA1024:Use properties where appropriate",
    Justification = "GetDebugView is a method for debugger display and should remain a method.",
    Scope = "member",
    Target = "~M:DropBear.Codex.Core.Results.Base.ResultBase.GetDebugView~System.String")]

// =====================================================================
// PERFORMANCE & DESIGN
// =====================================================================

[assembly: SuppressMessage("Performance", "CA1822:Mark members as static",
    Justification = "Instance method is intentional for potential future state usage.",
    Scope = "member",
    Target = "~M:DropBear.Codex.Core.Results.Errors.DefaultResultErrorHandler.ClassifyException(System.Exception)~DropBear.Codex.Core.Results.Base.ResultError")]

[assembly: SuppressMessage("Reliability", "CA2016:Forward the CancellationToken parameter",
    Justification = "CancellationToken is intentionally not forwarded in this specific async enumerable helper method.",
    Scope = "member",
    Target = "~M:DropBear.Codex.Core.Results.Async.AsyncEnumerableResult`2.Error(TError,System.Threading.CancellationToken)~DropBear.Codex.Core.Results.Async.AsyncEnumerableResult{T,TError}")]

[assembly: SuppressMessage("Usage", "CA1812:Avoid uninstantiated internal classes",
    Justification = "SecurityException is instantiated via reflection and exception handling mechanisms.",
    Scope = "type",
    Target = "~T:DropBear.Codex.Core.Results.Errors.DefaultResultErrorHandler+SecurityException")]
