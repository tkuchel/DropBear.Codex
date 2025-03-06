#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a specific snackbar to be displayed, including its content, duration, and actions.
/// </summary>
public sealed class SnackbarInstance
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SnackbarInstance" /> class
    ///     with a generated unique identifier.
    /// </summary>
    public SnackbarInstance()
    {
        Id = $"snackbar-{Guid.NewGuid():N}";
    }

    /// <summary>
    ///     Gets the unique identifier for this snackbar instance (e.g. "snackbar-&lt;guid&gt;").
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Gets or sets the title of the snackbar.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    ///     Gets or sets the message/body text of the snackbar.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     Gets or sets the visual type of the snackbar (e.g., Information, Success, Error).
    /// </summary>
    public SnackbarType Type { get; init; } = SnackbarType.Information;

    /// <summary>
    ///     Gets or sets the display duration in milliseconds.
    ///     Defaults to 5000 (5 seconds).
    /// </summary>
    public int Duration { get; init; } = 5000;

    /// <summary>
    ///     Gets or sets a value indicating whether the snackbar must be closed manually,
    ///     rather than automatically disappearing after <see cref="Duration" />.
    /// </summary>
    public bool RequiresManualClose { get; init; }

    /// <summary>
    ///     Gets or sets a list of actions (buttons) displayed on the snackbar.
    /// </summary>
    public List<SnackbarAction> Actions { get; init; } = [];


    /// <summary>
    ///     Gets or sets the time at which the snackbar was created.
    ///     Used to calculate the time remaining before the snackbar is automatically dismissed.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
