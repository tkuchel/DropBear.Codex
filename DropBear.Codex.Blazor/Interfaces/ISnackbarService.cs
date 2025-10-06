#region

using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Service for managing snackbar notifications in Blazor applications.
/// </summary>
public interface ISnackbarService
{
    /// <summary>
    ///     Event fired when a new snackbar is shown.
    /// </summary>
    event Func<SnackbarInstance, Task> OnShow;

    /// <summary>
    ///     Event fired when a snackbar is removed.
    /// </summary>
    event Func<string, Task> OnRemove;

    /// <summary>
    ///     Shows a snackbar notification.
    /// </summary>
    /// <param name="snackbar">The snackbar instance to show.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task Show(SnackbarInstance snackbar, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Shows a simple informational snackbar.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title.</param>
    /// <param name="duration">Duration in milliseconds (0 for manual close).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task ShowInfo(string message, string? title = null, int duration = 5000,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Shows a success snackbar.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title.</param>
    /// <param name="duration">Duration in milliseconds (0 for manual close).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task ShowSuccess(string message, string? title = null, int duration = 4000,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Shows a warning snackbar.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title.</param>
    /// <param name="duration">Duration in milliseconds (0 for manual close).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task ShowWarning(string message, string? title = null, int duration = 7000,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Shows an error snackbar (requires manual close by default).
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">Optional title.</param>
    /// <param name="duration">Duration in milliseconds (0 for manual close).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task ShowError(string message, string? title = null, int duration = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes a specific snackbar by ID.
    /// </summary>
    /// <param name="snackbarId">The ID of the snackbar to remove.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task RemoveSnackbar(string snackbarId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes all active snackbars.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the operation.</returns>
    Task RemoveAll(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all currently active snackbars.
    /// </summary>
    /// <returns>A collection of active snackbar instances.</returns>
    IReadOnlyList<SnackbarInstance> GetActiveSnackbars();

    /// <summary>
    ///     Gets the count of active snackbars.
    /// </summary>
    /// <returns>The number of active snackbars.</returns>
    int GetActiveCount();

    /// <summary>
    ///     Checks if a specific snackbar is currently active.
    /// </summary>
    /// <param name="snackbarId">The ID of the snackbar to check.</param>
    /// <returns>True if the snackbar is active, false otherwise.</returns>
    bool IsActive(string snackbarId);
}
