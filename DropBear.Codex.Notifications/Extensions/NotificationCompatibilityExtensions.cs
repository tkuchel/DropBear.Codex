#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Notifications.Enums;
using DropBear.Codex.Notifications.Errors;
using DropBear.Codex.Notifications.Interfaces;
using DropBear.Codex.Notifications.Models;

#endregion

namespace DropBear.Codex.Notifications.Extensions;

/// <summary>
///     Provides backward compatibility extensions to bridge the gap between
///     the strongly-typed Result pattern and the legacy Result pattern.
/// </summary>
public static class NotificationCompatibilityExtensions
{
    /// <summary>
    ///     Converts a strongly-typed Result{Notification, NotificationError} to a legacy Result{Notification}.
    /// </summary>
    /// <param name="result">The strongly-typed result.</param>
    /// <returns>A legacy result.</returns>
    public static Core.Results.Compatibility.Result<Notification> ToLegacyResult(
        this Result<Notification, NotificationError> result)
    {
        return result.IsSuccess
            ? Core.Results.Compatibility.Result<Notification>.Success(result.Value!)
            : Core.Results.Compatibility.Result<Notification>.Failure(result.Error!.Message, result.Exception);
    }

    /// <summary>
    ///     Converts a strongly-typed Result{Unit, NotificationError} to a legacy Result.
    /// </summary>
    /// <param name="result">The strongly-typed result.</param>
    /// <returns>A legacy result.</returns>
    public static Result ToLegacyResult(this Result<Unit, NotificationError> result)
    {
        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(result.Error!.Message, result.Exception);
    }

    /// <summary>
    ///     Backwards compatibility extension for the INotificationFactory interface.
    /// </summary>
    /// <param name="factory">The notification factory.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="severity">The notification severity.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="title">The notification title.</param>
    /// <param name="data">Additional data for the notification.</param>
    /// <returns>A legacy result containing the notification or an error message.</returns>
    public static Core.Results.Compatibility.Result<Notification> CreateNotificationLegacy(
        this INotificationFactory factory,
        Guid channelId,
        NotificationType type,
        NotificationSeverity severity,
        string message,
        string? title = null,
        Dictionary<string, object?>? data = null)
    {
        return factory.CreateNotification(channelId, type, severity, message, title, data).ToLegacyResult();
    }

    /// <summary>
    ///     Backwards compatibility extension for the INotificationService interface.
    /// </summary>
    /// <param name="service">The notification service.</param>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="isSensitive">Flag for marking the notification as sensitive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A legacy result indicating success or failure.</returns>
    public static async Task<Result> PublishNotificationLegacyAsync(
        this INotificationService service,
        Notification notification,
        bool isSensitive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await service.PublishNotificationAsync(notification, isSensitive, cancellationToken);
        return result.ToLegacyResult();
    }

    /// <summary>
    ///     Backwards compatibility extension for the INotificationService interface.
    /// </summary>
    /// <param name="service">The notification service.</param>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="isSensitive">Flag for marking the notification as sensitive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A legacy result indicating success or failure.</returns>
    public static async Task<Result> PublishGlobalNotificationLegacyAsync(
        this INotificationService service,
        Notification notification,
        bool isSensitive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await service.PublishGlobalNotificationAsync(notification, isSensitive, cancellationToken);
        return result.ToLegacyResult();
    }

    /// <summary>
    ///     Converts a NotificationError to a string message.
    /// </summary>
    /// <param name="error">The notification error.</param>
    /// <returns>A formatted error message.</returns>
    public static string ToDisplayMessage(this NotificationError error)
    {
        var context = !string.IsNullOrEmpty(error.Context) ? $" [{error.Context}]" : string.Empty;
        return $"{error.Message}{context}";
    }
}
