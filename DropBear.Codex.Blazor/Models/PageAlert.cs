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
    /// <exception cref="ArgumentException">Thrown when the title or message is null or empty.</exception>
    public PageAlert(string title, string message, AlertType type = AlertType.Information, bool isDismissible = true)
    {
        // Validate input parameters
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be null or empty", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or empty", nameof(message));
        }

        Id = Guid.NewGuid();
        Title = title;
        Message = message;
        Type = type;
        CreatedAt = DateTimeOffset.UtcNow;
        IsDismissible = isDismissible;
    }

    /// <summary>
    ///     Gets the unique identifier for the alert.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    ///     Gets the title of the alert.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Gets the message of the alert.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets the type of the alert.
    /// </summary>
    public AlertType Type { get; }

    /// <summary>
    ///     Gets the creation date and time of the alert in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    ///     Gets a value indicating whether the alert is dismissible.
    /// </summary>
    public bool IsDismissible { get; }
}
