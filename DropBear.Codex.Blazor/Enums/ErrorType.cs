namespace DropBear.Codex.Blazor.Enums;

/// <summary>
///     Represents the type of error that occurred during a data operation.
/// </summary>
public enum ErrorType
{
    /// <summary>
    ///     A general or unspecified error.
    /// </summary>
    General,

    /// <summary>
    ///     The operation timed out.
    /// </summary>
    Timeout,

    /// <summary>
    ///     The requested entity could not be found.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The user does not have permission to perform the operation.
    /// </summary>
    PermissionDenied,

    /// <summary>
    ///     The operation failed due to validation errors.
    /// </summary>
    Validation,

    /// <summary>
    ///     The operation was canceled by the user or system.
    /// </summary>
    Canceled
}
