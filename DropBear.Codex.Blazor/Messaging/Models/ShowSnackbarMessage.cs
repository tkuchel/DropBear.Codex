#region

using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Messaging.Models;

/// <summary>
///     Represents a message to show a snackbar notification.
/// </summary>
public record ShowSnackbarMessage
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ShowSnackbarMessage" /> class.
    /// </summary>
    /// <param name="options">The options for the snackbar notification.</param>
    public ShowSnackbarMessage(SnackbarNotificationOptions options)
    {
        Options = options;
    }

    /// <summary>
    ///     Gets the options for the snackbar notification.
    /// </summary>
    public SnackbarNotificationOptions Options { get; }
}
