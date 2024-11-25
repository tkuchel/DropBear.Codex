#region

using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

public interface ISnackbarService
{
    /// <summary>
    ///     Event that is triggered when a snackbar should be shown
    /// </summary>
    event Func<SnackbarInstance, Task>? OnShow;

    /// <summary>
    ///     Shows a custom snackbar instance
    /// </summary>
    /// <param name="snackbar">The snackbar instance to show</param>
    Task Show(SnackbarInstance snackbar);

    /// <summary>
    ///     Shows a success snackbar
    /// </summary>
    /// <param name="title">The title of the snackbar</param>
    /// <param name="message">The message to display</param>
    /// <param name="duration">Duration in milliseconds. Default is 5000ms (5 seconds)</param>
    /// <param name="actions">Optional list of actions that can be triggered from the snackbar</param>
    Task ShowSuccess(string title, string message, int duration = 5000, List<SnackbarAction>? actions = null);

    /// <summary>
    ///     Shows an error snackbar
    /// </summary>
    /// <param name="title">The title of the snackbar</param>
    /// <param name="message">The message to display</param>
    /// <param name="duration">Duration in milliseconds. Default is 0 (requires manual dismissal)</param>
    /// <param name="actions">Optional list of actions that can be triggered from the snackbar</param>
    Task ShowError(string title, string message, int duration = 0, List<SnackbarAction>? actions = null);

    /// <summary>
    ///     Shows a warning snackbar
    /// </summary>
    /// <param name="title">The title of the snackbar</param>
    /// <param name="message">The message to display</param>
    /// <param name="duration">Duration in milliseconds. Default is 8000ms (8 seconds)</param>
    /// <param name="actions">Optional list of actions that can be triggered from the snackbar</param>
    Task ShowWarning(string title, string message, int duration = 8000, List<SnackbarAction>? actions = null);

    /// <summary>
    ///     Shows an information snackbar
    /// </summary>
    /// <param name="title">The title of the snackbar</param>
    /// <param name="message">The message to display</param>
    /// <param name="duration">Duration in milliseconds. Default is 5000ms (5 seconds)</param>
    /// <param name="actions">Optional list of actions that can be triggered from the snackbar</param>
    Task ShowInformation(string title, string message, int duration = 5000, List<SnackbarAction>? actions = null);
}
