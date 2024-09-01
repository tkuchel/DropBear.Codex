#region

using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Interface for a service that manages snackbar notifications.
/// </summary>
public interface ISnackbarNotificationService
{
    /// <summary>
    ///     Event triggered when a new snackbar notification should be shown.
    /// </summary>
    event EventHandler<SnackbarNotificationEventArgs> OnShow;

    /// <summary>
    ///     Event triggered when all snackbar notifications should be hidden.
    /// </summary>
    event EventHandler OnHideAll;

    /// <summary>
    ///     Shows a snackbar notification with the specified message and options.
    /// </summary>
    /// <param name="message">The message to display in the snackbar.</param>
    /// <param name="type">The type of the snackbar notification.</param>
    /// <param name="duration">The duration in milliseconds for which the snackbar should be visible.</param>
    /// <param name="isDismissible">Indicates whether the snackbar notification is dismissible.</param>
    /// <param name="actionText">The text of the action button on the snackbar notification.</param>
    /// <param name="onAction">The action to perform when the action button is clicked.</param>
    /// <returns>A result indicating the success or failure of the operation.</returns>
    Result ShowAsync(string message, SnackbarType type = SnackbarType.Information, int duration = 5000,
        bool isDismissible = true, string actionText = "Dismiss", Func<Task>? onAction = null);

    /// <summary>
    ///     Shows a snackbar notification with the specified options.
    /// </summary>
    /// <param name="options">The options for the snackbar notification.</param>
    /// <returns>A result indicating the success or failure of the operation.</returns>
    Result Show(SnackbarNotificationOptions options);

    /// <summary>
    ///     Hides all currently visible snackbar notifications.
    /// </summary>
    /// <returns>A result indicating the success of the operation.</returns>
    Result HideAll();
}
