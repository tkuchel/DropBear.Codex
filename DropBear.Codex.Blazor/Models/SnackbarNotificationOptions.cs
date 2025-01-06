#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Specifies the options for displaying a snackbar notification (title, message, type, etc.).
/// </summary>
public abstract record SnackbarNotificationOptions
{
    /// <summary>
    ///     Initializes a new instance of <see cref="SnackbarNotificationOptions" />.
    /// </summary>
    /// <param name="title">The title of the snackbar notification.</param>
    /// <param name="message">The message body of the snackbar notification.</param>
    /// <param name="type">The type/category of the snackbar (Information, Success, etc.).</param>
    /// <param name="duration">How many milliseconds the snackbar is shown.</param>
    /// <param name="isDismissible">True if the snackbar can be dismissed by the user.</param>
    /// <param name="actionText">Label for an action button, if any.</param>
    /// <param name="onAction">Callback invoked if the action button is clicked.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown if <paramref name="title" />, <paramref name="message" />, or <paramref name="duration" /> are invalid.
    /// </exception>
    protected SnackbarNotificationOptions(
        string title,
        string message,
        SnackbarType type = SnackbarType.Information,
        int duration = 1500,
        bool isDismissible = true,
        string actionText = "Dismiss",
        Func<Task>? onAction = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be null or empty.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        if (duration <= 0)
        {
            throw new ArgumentException("Duration must be greater than zero.", nameof(duration));
        }

        Title = title;
        Message = message;
        Type = type;
        Duration = duration;
        IsDismissible = isDismissible;
        ActionText = actionText;
        OnAction = onAction ?? (() => Task.CompletedTask);
    }

    /// <summary>
    ///     Gets the title text displayed on the snackbar notification.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Gets the body message of the snackbar notification.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets the <see cref="SnackbarType" /> for visual appearance and classification.
    /// </summary>
    public SnackbarType Type { get; }

    /// <summary>
    ///     Gets the display duration in milliseconds.
    /// </summary>
    public int Duration { get; }

    /// <summary>
    ///     Gets a value indicating whether the snackbar can be manually dismissed by the user.
    /// </summary>
    public bool IsDismissible { get; }

    /// <summary>
    ///     Gets the text label for any action button.
    /// </summary>
    public string ActionText { get; }

    /// <summary>
    ///     Gets the action callback that executes when the action button is clicked.
    /// </summary>
    public Func<Task> OnAction { get; }
}
