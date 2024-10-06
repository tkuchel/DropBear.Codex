#region

using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Interface for the Snackbar Notification Service.
/// </summary>
public interface ISnackbarNotificationService
{
    /// <summary>
    ///     Occurs when a new snackbar should be shown.
    /// </summary>
    event AsyncEventHandler<SnackbarNotificationEventArgs>? OnShow;

    /// <summary>
    ///     Occurs when all snackbars should be hidden.
    /// </summary>
    event AsyncEventHandler<EventArgs>? OnHideAll;

    /// <summary>
    ///     Shows a snackbar notification with the specified message and options.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="type">The type of the snackbar.</param>
    /// <param name="duration">The duration to display the snackbar in milliseconds.</param>
    /// <param name="isDismissible">Indicates whether the snackbar notification is dismissible.</param>
    /// <param name="actionText">The text of the action button on the snackbar notification.</param>
    /// <param name="onAction">The action to perform when the action button is clicked.</param>
    /// <returns>A task representing the asynchronous operation, with a boolean indicating success.</returns>
    Task<bool> ShowAsync(
        string message,
        SnackbarType type = SnackbarType.Information,
        int duration = 5000,
        bool isDismissible = true,
        string actionText = "Dismiss",
        Func<Task>? onAction = null);

    /// <summary>
    ///     Shows a snackbar notification with the specified title, message, and options.
    /// </summary>
    /// <param name="title">The title to display.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="type">The type of the snackbar.</param>
    /// <param name="duration">The duration to display the snackbar in milliseconds.</param>
    /// <param name="isDismissible">Indicates whether the snackbar notification is dismissible.</param>
    /// <param name="actionText">The text of the action button on the snackbar notification.</param>
    /// <param name="onAction">The action to perform when the action button is clicked.</param>
    /// <returns>A task representing the asynchronous operation, with a boolean indicating success.</returns>
    Task<bool> ShowAsync(
        string title,
        string message,
        SnackbarType type = SnackbarType.Information,
        int duration = 5000,
        bool isDismissible = true,
        string actionText = "Dismiss",
        Func<Task>? onAction = null);

    /// <summary>
    ///     Hides all snackbar notifications.
    /// </summary>
    /// <returns>A task representing the asynchronous operation, with a boolean indicating success.</returns>
    Task<bool> HideAllAsync();
}
