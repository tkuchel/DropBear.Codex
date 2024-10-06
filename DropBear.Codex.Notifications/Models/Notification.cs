#region

using System.Collections.ObjectModel;
using DropBear.Codex.Notifications.Enums;

#endregion

namespace DropBear.Codex.Notifications.Models;

/// <summary>
///     Represents a notification with associated metadata and custom data.
/// </summary>
public sealed class Notification
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Notification" /> class.
    /// </summary>
    /// <param name="channelId">The channel ID associated with the notification.</param>
    /// <param name="type">The type of the notification.</param>
    /// <param name="severity">The severity of the notification.</param>
    /// <param name="message">The main message of the notification.</param>
    /// <param name="title">The optional title of the notification.</param>
    /// <param name="data">Custom data associated with the notification.</param>
    public Notification(
        Guid channelId,
        NotificationType type,
        NotificationSeverity severity,
        string message,
        string? title = null,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        if (channelId == Guid.Empty)
        {
            throw new ArgumentException("ChannelId cannot be empty.", nameof(channelId));
        }

        ChannelId = channelId;
        Type = type;
        Severity = severity;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Title = title;
        Timestamp = DateTime.UtcNow;
        Data = data ?? new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
    }

    /// <summary>
    ///     Gets the type of the notification.
    /// </summary>
    public NotificationType Type { get; }

    /// <summary>
    ///     Gets the severity of the notification.
    /// </summary>
    public NotificationSeverity Severity { get; }

    /// <summary>
    ///     Gets the main message of the notification.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets the optional title of the notification.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    ///     Gets the timestamp when the notification was created.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    ///     Gets the channel ID associated with the notification.
    /// </summary>
    public Guid ChannelId { get; }

    /// <summary>
    ///     Gets the custom data associated with the notification.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Data { get; }

    /// <summary>
    ///     Creates a new <see cref="Notification" /> instance with updated data.
    /// </summary>
    /// <param name="key">The key of the data to update.</param>
    /// <param name="value">The new value.</param>
    /// <returns>A new <see cref="Notification" /> instance with the updated data.</returns>
    public Notification WithUpdatedData(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        var newData = new Dictionary<string, object?>(Data) { [key] = value };

        return new Notification(ChannelId, Type, Severity, Message, Title, newData);
    }

    /// <summary>
    ///     Creates a new <see cref="Notification" /> instance with the specified key removed from the data.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>A new <see cref="Notification" /> instance without the specified data key.</returns>
    public Notification WithoutData(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        var newData = new Dictionary<string, object?>(Data);
        newData.Remove(key);

        return new Notification(ChannelId, Type, Severity, Message, Title, newData);
    }

    /// <summary>
    ///     Checks if the specified key exists in the custom data.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key exists; otherwise, false.</returns>
    public bool HasData(string key)
    {
        return Data.ContainsKey(key);
    }
}
