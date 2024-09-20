namespace DropBear.Codex.Notifications.Enums;

/// <summary>
///     Represents the type of alert to be displayed to the user.
/// </summary>
public enum AlertType
{
    /// <summary>
    ///     An alert displayed on a page.
    /// </summary>
    PageAlert,

    /// <summary>
    ///     A temporary notification typically shown at the bottom of the screen.
    /// </summary>
    Snackbar,

    /// <summary>
    ///     An alert showing the progress of a task.
    /// </summary>
    TaskProgress,

    /// <summary>
    ///     A system-wide alert.
    /// </summary>
    SystemAlert,

    /// <summary>
    ///     An error notification.
    /// </summary>
    Error,

    /// <summary>
    ///     An informational notification.
    /// </summary>
    Info,

    /// <summary>
    ///     A success notification.
    /// </summary>
    Success
}
