namespace DropBear.Codex.Core.Enums;

/// <summary>
///     Represents the severity level of an error.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    ///     Informational messages.
    /// </summary>
    Info = 0,

    /// <summary>
    ///     Low priority issues.
    /// </summary>
    Low = 1,

    /// <summary>
    ///     Medium priority issues.
    /// </summary>
    Medium = 2,

    /// <summary>
    ///     High priority issues requiring attention.
    /// </summary>
    High = 3,

    /// <summary>
    ///     Critical issues requiring immediate attention.
    /// </summary>
    Critical = 4
}
