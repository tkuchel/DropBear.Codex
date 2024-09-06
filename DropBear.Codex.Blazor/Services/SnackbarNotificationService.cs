#region

using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service to manage snackbar notifications.
/// </summary>
public sealed class SnackbarNotificationService : ISnackbarNotificationService
{
    /// <summary>
    ///     Event triggered when a new snackbar should be shown.
    /// </summary>
    public event EventHandler<SnackbarNotificationEventArgs>? OnShow;

    /// <summary>
    ///     Event triggered when all snackbars should be hidden.
    /// </summary>
    public event EventHandler? OnHideAll;

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
    public async Task<Result> ShowAsync(string message, SnackbarType type = SnackbarType.Information,
        int duration = 5000,
        bool isDismissible = true, string actionText = "Dismiss", Func<Task>? onAction = null)
    {
        var options = new SnackbarNotificationOptions(
            string.Empty,
            message,
            type,
            duration,
            isDismissible,
            actionText,
            onAction
        );

        return await ShowInternalAsync(options).ConfigureAwait(false);
    }

    /// <summary>
    ///     Hides all snackbar notifications.
    /// </summary>
    /// <returns>A result indicating the success of the operation.</returns>
    public Result HideAll()
    {
        try
        {
            OnHideAll?.Invoke(this, EventArgs.Empty);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure("Error hiding all snackbars", ex);
        }
    }

    /// <summary>
    ///     Shows a snackbar notification using the specified options.
    /// </summary>
    /// <param name="options">The options for the snackbar notification.</param>
    /// <returns>A task representing the result of the operation.</returns>
    private async Task<Result> ShowInternalAsync(SnackbarNotificationOptions options)
    {
        try
        {
            OnShow?.Invoke(this, new SnackbarNotificationEventArgs(options));
            return await Task.FromResult(Result.Success()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result.Failure("Error showing snackbar", ex);
        }
    }
}
