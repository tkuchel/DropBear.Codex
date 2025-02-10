#region

using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Defines a thread-safe service for managing snackbar notifications.
/// </summary>
public interface ISnackbarService : IAsyncDisposable
{
    /// <summary>
    ///     Occurs when a snackbar is shown.
    /// </summary>
    event Func<SnackbarInstance, Task>? OnShow;

    /// <summary>
    ///     Occurs when a snackbar is removed.
    /// </summary>
    event Func<string, Task>? OnRemove;

    /// <summary>
    ///     Shows a snackbar with thread-safe state management.
    /// </summary>
    /// <param name="snackbar">The snackbar to show.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
    /// <exception cref="ArgumentNullException">If snackbar is null.</exception>
    /// <exception cref="ObjectDisposedException">If service is disposed.</exception>
    Task<Result<Unit, SnackbarError>> Show(
        SnackbarInstance snackbar,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes a snackbar by ID.
    /// </summary>
    /// <param name="id">The snackbar ID to remove.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
    /// <exception cref="ArgumentException">If ID is invalid.</exception>
    /// <exception cref="ObjectDisposedException">If service is disposed.</exception>
    Task<Result<Unit, SnackbarError>> RemoveSnackbar(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a thread-safe snapshot of active snackbars.
    /// </summary>
    /// <returns>Read-only collection of active snackbars.</returns>
    /// <exception cref="ObjectDisposedException">If service is disposed.</exception>
    IReadOnlyCollection<SnackbarInstance> GetActiveSnackbars();

    /// <summary>
    ///     Shows a success snackbar.
    /// </summary>
    /// <param name="title">The snackbar title.</param>
    /// <param name="message">The snackbar message.</param>
    /// <param name="duration">Optional duration in milliseconds.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
    Task<Result<Unit, SnackbarError>> ShowSuccess(
        string title,
        string message,
        int duration = 5000,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Shows an error snackbar.
    /// </summary>
    /// <param name="title">The snackbar title.</param>
    /// <param name="message">The snackbar message.</param>
    /// <param name="duration">Optional duration (0 for manual close).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
    Task<Result<Unit, SnackbarError>> ShowError(
        string title,
        string message,
        int duration = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Shows a warning snackbar.
    /// </summary>
    /// <param name="title">The snackbar title.</param>
    /// <param name="message">The snackbar message.</param>
    /// <param name="duration">Optional duration in milliseconds.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
    Task<Result<Unit, SnackbarError>> ShowWarning(
        string title,
        string message,
        int duration = 8000,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Shows an information snackbar.
    /// </summary>
    /// <param name="title">The snackbar title.</param>
    /// <param name="message">The snackbar message.</param>
    /// <param name="duration">Optional duration in milliseconds.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Success result or error details.</returns>
    Task<Result<Unit, SnackbarError>> ShowInformation(
        string title,
        string message,
        int duration = 5000,
        CancellationToken cancellationToken = default);
}
