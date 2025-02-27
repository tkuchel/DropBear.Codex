#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Notifications.Errors;
using DropBear.Codex.Notifications.Models;

#endregion

namespace DropBear.Codex.Notifications.Interfaces;

/// <summary>
///     Defines methods for creating and publishing notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    ///     Publishes a notification to a specific channel.
    /// </summary>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="isSensitive">Flag for marking the notification as sensitive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result<Unit, NotificationError>> PublishNotificationAsync(
        Notification notification,
        bool isSensitive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Publishes a notification to the global channel.
    /// </summary>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="isSensitive">Flag for marking the notification as sensitive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result<Unit, NotificationError>> PublishGlobalNotificationAsync(
        Notification notification,
        bool isSensitive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Decrypts a previously encrypted notification.
    /// </summary>
    /// <param name="notification">The encrypted notification.</param>
    /// <returns>A decrypted notification.</returns>
    Result<Notification, NotificationError> DecryptNotification(Notification notification);
}
