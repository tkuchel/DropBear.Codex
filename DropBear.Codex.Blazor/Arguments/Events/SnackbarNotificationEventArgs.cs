

using DropBear.Codex.Blazor.Models;

namespace DropBear.Codex.Blazor.Arguments.Events;

/// <summary>
///     Event arguments for the snackbar notification event.
/// </summary>
public sealed class SnackbarNotificationEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarNotificationEventArgs" /> class.
    /// </summary>
    /// <param name="options">The snackbar notification options.</param>
    public SnackbarNotificationEventArgs(SnackbarNotificationOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    ///     Gets the options for the snackbar notification.
    /// </summary>
    public SnackbarNotificationOptions Options { get; }
}
