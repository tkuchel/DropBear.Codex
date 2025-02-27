#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Errors;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;

#endregion

namespace DropBear.Codex.Notifications.Services;

/// <summary>
///     Factory for creating notification objects with proper validation and error handling.
/// </summary>
public sealed class NotificationFactory : INotificationFactory
{
    private readonly NotificationPool _notificationPool;
    private readonly IResultTelemetry _telemetry;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotificationFactory" /> class.
    /// </summary>
    /// <param name="notificationPool">The notification object pool.</param>
    /// <param name="telemetry">The telemetry service for tracking result operations.</param>
    public NotificationFactory(NotificationPool notificationPool, IResultTelemetry telemetry)
    {
        _notificationPool = notificationPool ?? throw new ArgumentNullException(nameof(notificationPool));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    /// <summary>
    ///     Creates a new notification with the specified parameters.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="severity">The notification severity.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">The notification title.</param>
    /// <param name="data">Additional data for the notification.</param>
    /// <returns>A result containing the notification or an error message.</returns>
    public Result<Notification, NotificationError> CreateNotification(
        Guid channelId,
        NotificationType type,
        NotificationSeverity severity,
        string message,
        string? title = null,
        Dictionary<string, object?>? data = null)
    {
        try
        {
            // Validate input parameters
            if (channelId == Guid.Empty)
            {
                var error = new NotificationError("ChannelId cannot be empty.")
                    .WithContext(nameof(CreateNotification));
                _telemetry.TrackException(new ArgumentException(error.Message), ResultState.Failure, GetType());
                return Result<Notification, NotificationError>.Failure(error);
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                var error = new NotificationError("Message cannot be null or whitespace.")
                    .WithContext(nameof(CreateNotification));
                _telemetry.TrackException(new ArgumentException(error.Message), ResultState.Failure, GetType());
                return Result<Notification, NotificationError>.Failure(error);
            }

            if (type == NotificationType.NotSpecified)
            {
                var error = new NotificationError("NotificationType must be specified.")
                    .WithContext(nameof(CreateNotification));
                _telemetry.TrackException(new ArgumentException(error.Message), ResultState.Failure, GetType());
                return Result<Notification, NotificationError>.Failure(error);
            }

            if (severity == NotificationSeverity.NotSpecified)
            {
                var error = new NotificationError("NotificationSeverity must be specified.")
                    .WithContext(nameof(CreateNotification));
                _telemetry.TrackException(new ArgumentException(error.Message), ResultState.Failure, GetType());
                return Result<Notification, NotificationError>.Failure(error);
            }

            // Get a notification instance from the pool
            var notification = _notificationPool.Get(channelId, type, severity, message, title, data);

            _telemetry.TrackResultCreated(ResultState.Success, GetType());
            return Result<Notification, NotificationError>.Success(notification);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, GetType());
            return Result<Notification, NotificationError>.Failure(
                NotificationError.FromException(ex)
                    .WithContext(nameof(CreateNotification)));
        }
    }

    /// <summary>
    ///     Creates a new information notification.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">Optional notification title.</param>
    /// <param name="data">Optional additional data.</param>
    /// <returns>A result containing the notification or an error message.</returns>
    public Result<Notification, NotificationError> CreateInfoNotification(
        Guid channelId,
        string message,
        string? title = null,
        Dictionary<string, object?>? data = null)
    {
        return CreateNotification(
            channelId,
            NotificationType.Toast,
            NotificationSeverity.Information,
            message,
            title,
            data);
    }

    /// <summary>
    ///     Creates a new success notification.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">Optional notification title.</param>
    /// <param name="data">Optional additional data.</param>
    /// <returns>A result containing the notification or an error message.</returns>
    public Result<Notification, NotificationError> CreateSuccessNotification(
        Guid channelId,
        string message,
        string? title = null,
        Dictionary<string, object?>? data = null)
    {
        return CreateNotification(
            channelId,
            NotificationType.Toast,
            NotificationSeverity.Success,
            message,
            title,
            data);
    }

    /// <summary>
    ///     Creates a new warning notification.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">Optional notification title.</param>
    /// <param name="data">Optional additional data.</param>
    /// <returns>A result containing the notification or an error message.</returns>
    public Result<Notification, NotificationError> CreateWarningNotification(
        Guid channelId,
        string message,
        string? title = null,
        Dictionary<string, object?>? data = null)
    {
        return CreateNotification(
            channelId,
            NotificationType.PageAlert,
            NotificationSeverity.Warning,
            message,
            title,
            data);
    }

    /// <summary>
    ///     Creates a new error notification.
    /// </summary>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">Optional notification title.</param>
    /// <param name="data">Optional additional data.</param>
    /// <returns>A result containing the notification or an error message.</returns>
    public Result<Notification, NotificationError> CreateErrorNotification(
        Guid channelId,
        string message,
        string? title = null,
        Dictionary<string, object?>? data = null)
    {
        return CreateNotification(
            channelId,
            NotificationType.PageAlert,
            NotificationSeverity.Error,
            message,
            title,
            data);
    }
}
