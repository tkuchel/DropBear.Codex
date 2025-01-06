namespace DropBear.Codex.Notifications.Enums;

/// <summary>
///     Represents the severity level of a notification.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>
    ///     The notification severity is not specified.
    /// </summary>
    NotSpecified = 0,

    /// <summary>
    ///     An informational notification, typically used for neutral or positive information.
    /// </summary>
    Information,

    /// <summary>
    ///     A success notification, used to indicate a successful operation.
    /// </summary>
    Success,

    /// <summary>
    ///     A warning notification, used to alert the user to a potential issue.
    /// </summary>
    Warning,

    /// <summary>
    ///     An error notification, used to inform the user of a failure or critical issue.
    /// </summary>
    Error,

    /// <summary>
    ///     A critical notification, used for severe issues requiring immediate attention.
    /// </summary>
    Critical
}
