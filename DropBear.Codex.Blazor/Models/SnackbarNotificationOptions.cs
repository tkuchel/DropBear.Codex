#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents the options for displaying a snackbar notification.
/// </summary>
public class SnackbarNotificationOptions
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarNotificationOptions" /> class.
    /// </summary>
    /// <param name="title">The title of the snackbar notification.</param>
    /// <param name="message">The message of the snackbar notification.</param>
    /// <param name="type">The type of the snackbar notification.</param>
    /// <param name="duration">The duration in milliseconds for which the snackbar notification is displayed.</param>
    /// <param name="isDismissible">Indicates whether the snackbar notification is dismissible.</param>
    /// <param name="actionText">The text of the action button on the snackbar notification.</param>
    /// <param name="onAction">The action to perform when the action button is clicked.</param>
    public SnackbarNotificationOptions(
        string title,
        string message,
        SnackbarType type = SnackbarType.Information,
        int duration = 5000,
        bool isDismissible = true,
        string actionText = "Dismiss",
        Func<Task>? onAction = null)
    {
        Title = title;
        Message = message;
        Type = type;
        Duration = duration;
        IsDismissible = isDismissible;
        ActionText = actionText;
        OnAction = onAction ?? (() => Task.CompletedTask);
    }

    /// <summary>
    ///     Gets the title of the snackbar notification.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the message of the snackbar notification.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the type of the snackbar notification.
    /// </summary>
    public SnackbarType Type { get; init; } = SnackbarType.Information;

    /// <summary>
    ///     Gets the duration in milliseconds for which the snackbar notification is displayed.
    /// </summary>
    public int Duration { get; init; } = 5000;

    /// <summary>
    ///     Gets a value indicating whether the snackbar notification is dismissible.
    /// </summary>
    public bool IsDismissible { get; init; } = true;

    /// <summary>
    ///     Gets the text of the action button on the snackbar notification.
    /// </summary>
    public string ActionText { get; init; } = "Dismiss";

    /// <summary>
    ///     Gets the action to perform when the action button is clicked.
    /// </summary>
    public Func<Task> OnAction { get; init; } = () => Task.CompletedTask;
}
