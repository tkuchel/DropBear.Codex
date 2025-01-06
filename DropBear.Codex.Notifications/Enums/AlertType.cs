namespace DropBear.Codex.Notifications.Enums;

/// <summary>
///     Represents the type of notification to be displayed.
/// </summary>
public enum NotificationType
{
    /// <summary>
    ///     The notification type is not specified.
    /// </summary>
    NotSpecified = 0,

    /// <summary>
    ///     An alert displayed on the page, typically more prominent.
    /// </summary>
    PageAlert,

    /// <summary>
    ///     A brief, non-modal notification that shows for a short time.
    /// </summary>
    Toast,

    /// <summary>
    ///     A notification indicating the progress of a task.
    /// </summary>
    TaskProgress
}
