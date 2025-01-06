namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an actionable button on a snackbar notification.
/// </summary>
public abstract class SnackbarAction
{
    /// <summary>
    ///     Gets or sets the label (text) displayed on the action button.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a callback invoked when the user clicks this action.
    ///     If null, no click action is performed.
    /// </summary>
    public Func<Task>? OnClick { get; set; }
}
