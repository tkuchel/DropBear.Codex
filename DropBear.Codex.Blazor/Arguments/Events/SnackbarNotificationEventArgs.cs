using DropBear.Codex.Blazor.Models;

namespace DropBear.Codex.Blazor.Arguments.Events;

/// <summary>
///     Provides data for the snackbar notification event.
/// </summary>
public sealed class SnackbarNotificationEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarNotificationEventArgs" /> class.
    /// </summary>
    /// <param name="options">The options for the snackbar notification.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="options"/> argument is null.</exception>
    public SnackbarNotificationEventArgs(SnackbarNotificationOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    ///     Gets the snackbar notification options used to configure the notification behavior and appearance.
    /// </summary>
    public SnackbarNotificationOptions Options { get; }
}
