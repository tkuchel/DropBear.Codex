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
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="channelId" /> is empty or
    ///     <paramref name="message" /> is null.
    /// </exception>
    public Notification(
        Guid channelId,
        NotificationType type,
        NotificationSeverity severity,
        string message,
        string? title = null,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        Initialize(channelId, type, severity, message, title, data);
    }

    /// <summary>
    ///     Gets the type of the notification.
    /// </summary>
    public NotificationType Type { get; private set; }

    /// <summary>
    ///     Gets the severity of the notification.
    /// </summary>
    public NotificationSeverity Severity { get; private set; }

    /// <summary>
    ///     Gets the main message of the notification.
    /// </summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    ///     Gets the optional title of the notification.
    /// </summary>
    public string? Title { get; private set; }

    /// <summary>
    ///     Gets the timestamp when the notification was created.
    /// </summary>
    public DateTime Timestamp { get; private set; }

    /// <summary>
    ///     Gets the channel ID associated with the notification.
    /// </summary>
    public Guid ChannelId { get; private set; }

    /// <summary>
    ///     Gets the custom data associated with the notification.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Data { get; private set; } =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(0, StringComparer.Ordinal));

    /// <summary>
    ///     Initializes the notification with the specified values.
    ///     This method is primarily intended to be used by the object pool.
    /// </summary>
    /// <param name="channelId">The channel ID associated with the notification.</param>
    /// <param name="type">The type of the notification.</param>
    /// <param name="severity">The severity of the notification.</param>
    /// <param name="message">The main message of the notification.</param>
    /// <param name="title">The optional title of the notification.</param>
    /// <param name="data">Custom data associated with the notification.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="channelId" /> is empty or
    ///     <paramref name="message" /> is null.
    /// </exception>
    internal void Initialize(
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
        Data = data ?? new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(0, StringComparer.Ordinal));
    }

    /// <summary>
    ///     Resets the notification to its initial state.
    ///     This method is primarily intended to be used by the object pool.
    /// </summary>
    internal void Reset()
    {
        ChannelId = Guid.Empty;
        Type = NotificationType.NotSpecified;
        Severity = NotificationSeverity.NotSpecified;
        Message = string.Empty;
        Title = null;
        Timestamp = DateTime.UtcNow;
        Data = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(0, StringComparer.Ordinal));
    }

    /// <summary>
    ///     Creates a new <see cref="Notification" /> instance with updated data.
    /// </summary>
    /// <param name="key">The key of the data to update.</param>
    /// <param name="value">The new value.</param>
    /// <returns>A new <see cref="Notification" /> instance with the updated data.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="key" /> is null or whitespace.
    /// </exception>
    public Notification WithUpdatedData(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        var newData = new Dictionary<string, object?>(Data, StringComparer.Ordinal) { [key] = value };

        return new Notification(ChannelId, Type, Severity, Message, Title, newData);
    }

    /// <summary>
    ///     Creates a new <see cref="Notification" /> instance with the specified key removed from the data.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>A new <see cref="Notification" /> instance without the specified data key.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="key" /> is null or whitespace.
    /// </exception>
    public Notification WithoutData(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        var newData = new Dictionary<string, object?>(Data, StringComparer.Ordinal);
        newData.Remove(key);

        return new Notification(ChannelId, Type, Severity, Message, Title, newData);
    }

    /// <summary>
    ///     Creates a new <see cref="Notification" /> instance with updated message.
    /// </summary>
    /// <param name="message">The new message.</param>
    /// <returns>A new <see cref="Notification" /> instance with the updated message.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="message" /> is null.
    /// </exception>
    public Notification WithMessage(string message)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        return new Notification(ChannelId, Type, Severity, message, Title, Data);
    }

    /// <summary>
    ///     Creates a new <see cref="Notification" /> instance with updated title.
    /// </summary>
    /// <param name="title">The new title.</param>
    /// <returns>A new <see cref="Notification" /> instance with the updated title.</returns>
    public Notification WithTitle(string? title)
    {
        return new Notification(ChannelId, Type, Severity, Message, title, Data);
    }

    /// <summary>
    ///     Checks if the specified key exists in the custom data.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key exists; otherwise, false.</returns>
    public bool HasData(string key)
    {
        return !string.IsNullOrEmpty(key) && Data.ContainsKey(key);
    }

    /// <summary>
    ///     Gets a value from the data dictionary as the specified type.
    /// </summary>
    /// <typeparam name="T">The type to convert the value to.</typeparam>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="defaultValue">The default value if the key is not found or the value cannot be converted.</param>
    /// <returns>The value of the specified key, or the default value if not found or not convertible.</returns>
    public T? GetDataValue<T>(string key, T? defaultValue = default)
    {
        if (string.IsNullOrEmpty(key) || !Data.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        try
        {
            if (value is T typedValue)
            {
                return typedValue;
            }

            // Try to convert using Convert class if applicable
            if (typeof(T).IsPrimitive && value != null)
            {
                return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // Ignore conversion errors and return default value
        }

        return defaultValue;
    }
}
