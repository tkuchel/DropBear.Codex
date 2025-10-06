namespace DropBear.Codex.Blazor.Enums;

/// <summary>
///     Defines the visual type and semantic meaning of a snackbar notification.
/// </summary>
public enum SnackbarType
{
    /// <summary>
    ///     General informational message (blue theme).
    /// </summary>
    Information = 0,

    /// <summary>
    ///     Success notification (green theme).
    /// </summary>
    Success = 1,

    /// <summary>
    ///     Warning message (amber/orange theme).
    /// </summary>
    Warning = 2,

    /// <summary>
    ///     Error or failure notification (red theme).
    /// </summary>
    Error = 3
}
