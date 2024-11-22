#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an alert displayed on a page.
/// </summary>
public sealed class PageAlert : IEquatable<PageAlert>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PageAlert" /> class.
    /// </summary>
    /// <param name="title">The title of the alert.</param>
    /// <param name="message">The message of the alert.</param>
    /// <param name="type">The type of the alert.</param>
    /// <param name="isDismissible">Indicates whether the alert is dismissible.</param>
    /// <param name="channelId">The channel identifier for the alert. If null, alert is global.</param>
    /// <exception cref="ArgumentException">Thrown when the title or message is null or empty.</exception>
    public PageAlert(
        string title,
        string message,
        AlertType type = AlertType.Information,
        bool isDismissible = true,
        string? channelId = null)
    {
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
        ChannelId = channelId;
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

    /// <summary>
    ///     Gets the channel identifier for the alert. Null indicates a global alert.
    /// </summary>
    public string? ChannelId { get; }

    /// <summary>
    ///     Determines whether the specified alert is equal to the current alert.
    /// </summary>
    /// <param name="other">The alert to compare with the current alert.</param>
    /// <returns>true if the specified alert is equal to the current alert; otherwise, false.</returns>
    public bool Equals(PageAlert? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Id.Equals(other.Id);
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current alert.
    /// </summary>
    /// <param name="obj">The object to compare with the current alert.</param>
    /// <returns>true if the specified object is equal to the current alert; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is PageAlert alert && Equals(alert);
    }

    /// <summary>
    ///     Returns a hash code for the current alert.
    /// </summary>
    /// <returns>A hash code for the current alert.</returns>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>
    ///     Returns a string that represents the current alert.
    /// </summary>
    /// <returns>A string that represents the current alert.</returns>
    public override string ToString()
    {
        return $"Alert {Id} - {Type}: {Title} ({(ChannelId != null ? $"Channel: {ChannelId}" : "Global")})";
    }
}
