using System.Runtime.InteropServices;
using DropBear.Codex.Core.Enums;

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents classification information for an error.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ErrorClassification(
    ErrorCategory Category,
    ErrorSeverity Severity,
    bool IsTransient,
    bool IsRetryable)
{
    /// <summary>
    ///     Gets a value indicating whether this error should be logged.
    /// </summary>
    public bool ShouldLog => Severity >= ErrorSeverity.Medium;

    /// <summary>
    ///     Gets a value indicating whether this error should alert operations.
    /// </summary>
    public bool ShouldAlert => Severity >= ErrorSeverity.High;
}
