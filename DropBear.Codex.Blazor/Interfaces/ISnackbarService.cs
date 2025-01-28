#region

using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
/// Provides services for managing and displaying snackbar notifications in a Blazor application.
/// This service handles the lifecycle of snackbars, including showing, removing, and tracking active instances.
/// </summary>
public interface ISnackbarService : IAsyncDisposable
{
    /// <summary>
    /// Event that is triggered when a new snackbar is about to be shown.
    /// Subscribers can use this to handle the UI presentation of the snackbar.
    /// </summary>
    event Func<SnackbarInstance, Task>? OnShow;

    /// <summary>
    /// Event that is triggered when a snackbar is about to be removed.
    /// Subscribers can use this to handle cleanup and UI removal of the snackbar.
    /// </summary>
    event Func<string, Task>? OnRemove;

    /// <summary>
    /// Shows a custom snackbar instance.
    /// </summary>
    /// <param name="snackbar">The snackbar instance to display.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    /// <remarks>
    /// If the maximum number of active snackbars is reached, this method will attempt to remove
    /// the oldest non-error snackbar before showing the new one.
    /// </remarks>
    Task<Result<Unit, SnackbarError>> Show(SnackbarInstance snackbar);

    /// <summary>
    /// Shows a success-type snackbar with the specified message.
    /// </summary>
    /// <param name="title">The title of the snackbar.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="duration">The duration in milliseconds to show the snackbar. Defaults to 5000ms (5 seconds).</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    Task<Result<Unit, SnackbarError>> ShowSuccess(string title, string message, int duration = 5000);

    /// <summary>
    /// Shows an error-type snackbar with the specified message.
    /// </summary>
    /// <param name="title">The title of the snackbar.</param>
    /// <param name="message">The error message to display.</param>
    /// <param name="duration">The duration in milliseconds to show the snackbar. Defaults to 0, which means it requires manual dismissal.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    /// <remarks>
    /// Error snackbars with duration of 0 will require manual user interaction to dismiss.
    /// These snackbars are not automatically removed when reaching the maximum snackbar limit.
    /// </remarks>
    Task<Result<Unit, SnackbarError>> ShowError(string title, string message, int duration = 0);

    /// <summary>
    /// Shows a warning-type snackbar with the specified message.
    /// </summary>
    /// <param name="title">The title of the snackbar.</param>
    /// <param name="message">The warning message to display.</param>
    /// <param name="duration">The duration in milliseconds to show the snackbar. Defaults to 8000ms (8 seconds).</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    Task<Result<Unit, SnackbarError>> ShowWarning(string title, string message, int duration = 8000);

    /// <summary>
    /// Shows an information-type snackbar with the specified message.
    /// </summary>
    /// <param name="title">The title of the snackbar.</param>
    /// <param name="message">The informational message to display.</param>
    /// <param name="duration">The duration in milliseconds to show the snackbar. Defaults to 5000ms (5 seconds).</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    Task<Result<Unit, SnackbarError>> ShowInformation(string title, string message, int duration = 5000);

    /// <summary>
    /// Removes a specific snackbar by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the snackbar to remove.</param>
    /// <returns>A result indicating success or failure with error details.</returns>
    /// <remarks>
    /// This method will trigger the OnRemove event if the snackbar is successfully removed.
    /// If the snackbar with the specified ID is not found, returns a failure result.
    /// </remarks>
    Task<Result<Unit, SnackbarError>> RemoveSnackbar(string id);

    /// <summary>
    /// Gets a read-only collection of all currently active snackbars.
    /// </summary>
    /// <returns>An immutable collection of active snackbar instances.</returns>
    /// <remarks>
    /// The returned collection represents a snapshot of the current state and may change
    /// as new snackbars are added or removed.
    /// </remarks>
    IReadOnlyCollection<SnackbarInstance> GetActiveSnackbars();
}
