#region

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Models;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Notifications.Services;

/// <summary>
///     Provides an object pooling mechanism for Notification objects to improve performance
///     and reduce memory allocations in high-throughput scenarios.
/// </summary>
public sealed class NotificationPool
{
    private readonly ConcurrentDictionary<NotificationType, ObjectPool<Notification>> _pools = new();

    /// <summary>
    ///     Gets a Notification instance from the pool, initializing it with the provided parameters.
    /// </summary>
    /// <param name="channelId">The channel ID for the notification.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="severity">The notification severity.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">Optional notification title.</param>
    /// <param name="data">Optional notification data.</param>
    /// <returns>An initialized Notification instance from the pool.</returns>
    /// <exception cref="ArgumentException">Thrown if channelId is empty or message is null/empty.</exception>
    public Notification Get(
        Guid channelId,
        NotificationType type,
        NotificationSeverity severity,
        string message,
        string? title = null,
        IDictionary<string, object?>? data = null)
    {
        if (channelId == Guid.Empty)
        {
            throw new ArgumentException("ChannelId cannot be empty.", nameof(channelId));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));
        }

        // Get or create a pool for this notification type
        var pool = _pools.GetOrAdd(type, _ => new DefaultObjectPool<Notification>(new NotificationPoolPolicy()));

        // Get an instance from the pool
        var notification = pool.Get();

        // Copy data dictionary if provided
        IReadOnlyDictionary<string, object?>? readOnlyData = null;
        if (data != null)
        {
            readOnlyData = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(data, StringComparer.Ordinal));
        }

        // Initialize the notification with the provided parameters
        notification.Initialize(channelId, type, severity, message, title, readOnlyData);

        return notification;
    }

    /// <summary>
    ///     Returns a Notification instance to the pool for reuse.
    /// </summary>
    /// <param name="notification">The notification to return to the pool.</param>
    /// <exception cref="ArgumentNullException">Thrown if notification is null.</exception>
    public void Return(Notification notification)
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        if (_pools.TryGetValue(notification.Type, out var pool))
        {
            pool.Return(notification);
        }
    }

    // Internal policy for the object pool
    private class NotificationPoolPolicy : IPooledObjectPolicy<Notification>
    {
        /// <summary>
        ///     Creates a new Notification instance for the pool.
        /// </summary>
        /// <returns>A new notification with default values.</returns>
        public Notification Create()
        {
            // Create a notification with valid dummy values
            // These will be overwritten when Get() is called and Initialize() is invoked
            // Note: We use valid values here because the Notification constructor validates immediately
            return new Notification(
                Guid.NewGuid(),
                NotificationType.Toast,
                NotificationSeverity.Information,
                "Pooled notification");
        }

        /// <summary>
        ///     Prepares a Notification instance for return to the pool.
        /// </summary>
        /// <param name="obj">The notification being returned.</param>
        /// <returns>True if the object can be reused; otherwise, false.</returns>
        public bool Return(Notification obj)
        {
            try
            {
                // Reset the notification to a clean state
                obj.Reset();
                return true;
            }
            catch
            {
                // If reset fails, don't reuse this instance
                return false;
            }
        }
    }
}
