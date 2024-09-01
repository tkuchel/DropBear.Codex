#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an alert displayed on a page.
/// </summary>
public sealed class PageAlert
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PageAlert" /> class.
    /// </summary>
    /// <param name="title">The title of the alert.</param>
    /// <param name="message">The message of the alert.</param>
    /// <param name="type">The type of the alert.</param>
    /// <param name="isDismissible">Indicates whether the alert is dismissible.</param>
    public PageAlert(string title, string message, AlertType type = AlertType.Information, bool isDismissible = true)
    {
        Id = Guid.NewGuid();
        Title = title;
        Message = message;
        Type = type;
        CreatedAt = DateTime.UtcNow;
        IsDismissible = isDismissible;
    }

    /// <summary>
    ///     Gets the unique identifier for the alert.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    ///     Gets the title of the alert.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the message of the alert.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the type of the alert.
    /// </summary>
    public AlertType Type { get; init; } = AlertType.Information;

    /// <summary>
    ///     Gets the creation date and time of the alert.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the alert is dismissible.
    /// </summary>
    public bool IsDismissible { get; init; }
}
