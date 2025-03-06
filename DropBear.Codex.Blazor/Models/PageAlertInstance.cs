#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an alert instance displayed on a page.
/// </summary>
public class PageAlertInstance
{
    /// <summary>
    ///     Gets or sets the unique identifier for this alert.
    ///     Required for distinguishing multiple alerts.
    /// </summary>
    public required string Id { get; init; } = $"alert-{Guid.NewGuid():N}";

    /// <summary>
    ///     Gets or sets the title of the alert.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    ///     Gets or sets the message body of the alert.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     Gets or sets the type of alert (Success, Error, Warning, Info, etc.).
    /// </summary>
    public PageAlertType Type { get; init; }

    /// <summary>
    ///     Gets or sets the duration (in milliseconds) for which the alert is visible.
    ///     If null, a default duration may be used by the UI.
    /// </summary>
    public int? Duration { get; init; }

    /// <summary>
    ///     Gets or sets a value indicating whether the alert is permanent
    ///     (i.e., does not automatically disappear).
    /// </summary>
    public bool IsPermanent { get; init; }

    /// <summary>
    ///     Gets or sets the time at which the snackbar was created.
    ///     Used to calculate the time remaining before the snackbar is automatically dismissed.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
