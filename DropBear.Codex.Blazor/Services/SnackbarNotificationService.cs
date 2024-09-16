#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Messaging.Models;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using MessagePipe;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service to manage snackbar notifications.
/// </summary>
public sealed class SnackbarNotificationService : ISnackbarNotificationService
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<SnackbarNotificationService>();
    private readonly IPublisher<HideAllSnackbarsMessage> _hideAllPublisher;
    private readonly IPublisher<ShowSnackbarMessage> _showPublisher;

    public SnackbarNotificationService(
        IPublisher<ShowSnackbarMessage> showPublisher,
        IPublisher<HideAllSnackbarsMessage> hideAllPublisher)
    {
        _showPublisher = showPublisher;
        _hideAllPublisher = hideAllPublisher;
        Logger.Debug("SnackbarNotificationService initialized.");
    }

    /// <summary>
    ///     Shows a snackbar notification with the specified message and options.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="type">The type of the snackbar.</param>
    /// <param name="duration">The duration to display the snackbar in milliseconds.</param>
    /// <param name="isDismissible">Indicates whether the snackbar notification is dismissible.</param>
    /// <param name="actionText">The text of the action button on the snackbar notification.</param>
    /// <param name="onAction">The action to perform when the action button is clicked.</param>
    /// <returns>A task representing the result of the operation.</returns>
    public Task<Result> ShowAsync(string message, SnackbarType type = SnackbarType.Information,
        int duration = 5000, bool isDismissible = true, string actionText = "Dismiss", Func<Task>? onAction = null)
    {
        var options = new SnackbarNotificationOptions(
            "Notification",
            message,
            type,
            duration,
            isDismissible,
            actionText,
            onAction
        );

        return ShowInternalAsync(options);
    }

    /// <summary>
    ///     Shows a snackbar notification with the specified message and options.
    /// </summary>
    /// <param name="title">The title to display.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="type">The type of the snackbar.</param>
    /// <param name="duration">The duration to display the snackbar in milliseconds.</param>
    /// <param name="isDismissible">Indicates whether the snackbar notification is dismissible.</param>
    /// <param name="actionText">The text of the action button on the snackbar notification.</param>
    /// <param name="onAction">The action to perform when the action button is clicked.</param>
    /// <returns>A task representing the result of the operation.</returns>
    public Task<Result> ShowAsync(string title, string message, SnackbarType type = SnackbarType.Information,
        int duration = 5000, bool isDismissible = true, string actionText = "Dismiss", Func<Task>? onAction = null)
    {
        var options = new SnackbarNotificationOptions(
            title,
            message,
            type,
            duration,
            isDismissible,
            actionText,
            onAction
        );

        return ShowInternalAsync(options);
    }

    /// <summary>
    ///     Hides all snackbar notifications.
    /// </summary>
    /// <returns>A task representing the result of the operation.</returns>
    public Task<Result> HideAllAsync()
    {
        try
        {
            _hideAllPublisher.Publish(new HideAllSnackbarsMessage());
            Logger.Debug("HideAllSnackbarsMessage published.");
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error publishing HideAllSnackbarsMessage.");
            return Task.FromResult(Result.Failure("Error hiding all snackbars", ex));
        }
    }

    /// <summary>
    ///     Shows a snackbar notification using the specified options.
    /// </summary>
    /// <param name="options">The options for the snackbar notification.</param>
    /// <returns>A task representing the result of the operation.</returns>
    private Task<Result> ShowInternalAsync(SnackbarNotificationOptions options)
    {
        try
        {
            _showPublisher.Publish(new ShowSnackbarMessage(options));
            Logger.Debug("ShowSnackbarMessage published with options: {@Options}", options);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error publishing ShowSnackbarMessage.");
            return Task.FromResult(Result.Failure("Error showing snackbar", ex));
        }
    }
}
