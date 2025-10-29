#region

using DropBear.Codex.Core.Results;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Errors;
using DropBear.Codex.Notifications.Filters;
using DropBear.Codex.Notifications.Models;

#endregion

namespace DropBear.Codex.Notifications.Interfaces;

/// <summary>
///     Repository interface for notification persistence operations.
///     All methods return Results to enable proper error handling without exceptions.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    ///     Retrieves a notification by its unique identifier.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the notification record if found, or an error.</returns>
    Task<Result<NotificationRecord?, NotificationError>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves notifications for a user based on filter criteria.
    /// </summary>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the notifications and total count, or an error.</returns>
    Task<Result<(IReadOnlyList<NotificationRecord> Notifications, int TotalCount), NotificationError>> GetForUserAsync(
        NotificationFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a new notification record.
    /// </summary>
    /// <param name="notification">The notification to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, NotificationError>> CreateAsync(
        NotificationRecord notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks a notification as read.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, NotificationError>> MarkAsReadAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Marks all notifications for a user as read.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, NotificationError>> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Dismisses a notification.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, NotificationError>> DismissAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Dismisses all notifications for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, NotificationError>> DismissAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the count of unread notifications for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the unread count, or an error.</returns>
    Task<Result<int, NotificationError>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves notification preferences for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the preferences if found, or an error.</returns>
    Task<Result<NotificationPreferences?, NotificationError>> GetUserPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves notification preferences for a user.
    /// </summary>
    /// <param name="preferences">The preferences to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, NotificationError>> SaveUserPreferencesAsync(
        NotificationPreferences preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves recent notifications for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="count">Maximum number of notifications to retrieve.</param>
    /// <param name="unreadOnly">Whether to retrieve only unread notifications.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the recent notifications, or an error.</returns>
    Task<Result<IReadOnlyList<NotificationRecord>, NotificationError>> GetRecentNotificationsAsync(
        Guid userId,
        int count = 5,
        bool unreadOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes old notifications beyond the specified retention period.
    /// </summary>
    /// <param name="daysToKeep">Number of days to retain notifications.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing true if any notifications were deleted, or an error.</returns>
    Task<Result<bool, NotificationError>> DeleteOldNotificationsAsync(
        int daysToKeep = 90,
        CancellationToken cancellationToken = default);
}
