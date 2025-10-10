namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents error categories for classification across results, telemetry, and handling policies.
/// </summary>
/// <remarks>
///     Suggested handling:
///     <list type="bullet">
///         <item>
///             <description><see cref="Validation" /> / <see cref="Authorization" />: do not retry; fix input/credentials.</description>
///         </item>
///         <item>
///             <description>
///                 <see cref="Timeout" /> / <see cref="Network" /> / <see cref="IO" />: consider retry with
///                 backoff.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="InvalidOperation" /> / <see cref="Critical" />: developer or system intervention
///                 required.
///             </description>
///         </item>
///         <item>
///             <description><see cref="Cancelled" />: expected user/system cancellation; usually not treated as a failure.</description>
///         </item>
///     </list>
/// </remarks>
public enum ErrorCategory
{
    /// <summary>
    ///     The error could not be classified. Use when the underlying cause is unknown
    ///     or an appropriate category has not been assigned yet.
    /// </summary>
    Unknown,

    /// <summary>
    ///     The request or input failed validation (e.g., missing/invalid fields, format errors).
    ///     Caller action is required to correct and resubmit. Not retriable without changes.
    /// </summary>
    Validation,

    /// <summary>
    ///     The caller is unauthenticated or lacks permission (analogous to 401/403 scenarios).
    ///     Resolve by authenticating or adjusting authorization. Do not retry blindly.
    /// </summary>
    Authorization,

    /// <summary>
    ///     The operation exceeded the allowed time window. Often transient and may succeed
    ///     on retry with backoff or increased timeout.
    /// </summary>
    Timeout,

    /// <summary>
    ///     The operation was intentionally cancelled (e.g., via <c>CancellationToken</c>).
    ///     Typically not considered a failure for telemetry or alerts.
    /// </summary>
    Cancelled,

    /// <summary>
    ///     The operation was invoked in an invalid state or sequence (programming/logic error).
    ///     Not retriable at runtime; requires code or workflow correction.
    /// </summary>
    InvalidOperation,

    /// <summary>
    ///     An input/output error occurred (e.g., file system, disk, stream access).
    ///     May be transient (locks, contention) or persistent (missing path, permissions).
    /// </summary>
    IO,

    /// <summary>
    ///     A network-related error occurred (e.g., DNS, connectivity, socket, TLS).
    ///     Often transient; apply retry with jitter/backoff where safe.
    /// </summary>
    Network,

    /// <summary>
    ///     A severe or unrecoverable condition indicating potential data loss, corruption,
    ///     or system inconsistency. Requires immediate attention and escalation.
    /// </summary>
    Critical,

    /// <summary>
    ///     A general, non-specific failure used when a known error occurs but does not fit
    ///     a more precise category. Prefer more specific categories when possible.
    /// </summary>
    General
}
