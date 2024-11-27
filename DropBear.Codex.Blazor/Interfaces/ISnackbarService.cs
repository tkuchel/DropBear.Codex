#region

using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Interface for snackbar notification service
/// </summary>
public interface ISnackbarService : IDisposable
{
    /// <summary>
    ///     Event that fires when a snackbar needs to be shown
    /// </summary>
    event Func<SnackbarInstance, Task>? OnShow;

    /// <summary>
    ///     Shows a snackbar with the specified instance
    /// </summary>
    /// <param name="snackbar">The snackbar instance to show</param>
    /// <returns>A result indicating success or failure</returns>
    Task<Result<Unit, SnackbarError>> Show(SnackbarInstance snackbar);

    /// <summary>
    ///     Shows a success snackbar
    /// </summary>
    /// <param name="title">The title of the snackbar</param>
    /// <param name="message">The message to display</param>
    /// <param name="duration">Duration in milliseconds (default 5000ms)</param>
    /// <param name="actions">Optional list of actions for the snackbar</param>
    /// <returns>A result indicating success or failure</returns>
    Task<Result<Unit, SnackbarError>> ShowSuccess(
        string title,
        string message,
        int duration = 5000,
        List<SnackbarAction>? actions = null);

    /// <summary>
    ///     Shows an error snackbar
    /// </summary>
    /// <param name="title">The title of the snackbar</param>
    /// <param name="message">The error message to display</param>
    /// <param name="duration">Duration in milliseconds (0 for manual close)</param>
    /// <param name="actions">Optional list of actions for the snackbar</param>
    /// <returns>A result indicating success or failure</returns>
    Task<Result<Unit, SnackbarError>> ShowError(
        string title,
        string message,
        int duration = 0,
        List<SnackbarAction>? actions = null);

    /// <summary>
    ///     Shows a warning snackbar
    /// </summary>
    /// <param name="title">The title of the snackbar</param>
    /// <param name="message">The warning message to display</param>
    /// <param name="duration">Duration in milliseconds (default 8000ms)</param>
    /// <param name="actions">Optional list of actions for the snackbar</param>
    /// <returns>A result indicating success or failure</returns>
    Task<Result<Unit, SnackbarError>> ShowWarning(
        string title,
        string message,
        int duration = 8000,
        List<SnackbarAction>? actions = null);

    /// <summary>
    ///     Shows an information snackbar
    /// </summary>
    /// <param name="title">The title of the snackbar</param>
    /// <param name="message">The information message to display</param>
    /// <param name="duration">Duration in milliseconds (default 5000ms)</param>
    /// <param name="actions">Optional list of actions for the snackbar</param>
    /// <returns>A result indicating success or failure</returns>
    Task<Result<Unit, SnackbarError>> ShowInformation(
        string title,
        string message,
        int duration = 5000,
        List<SnackbarAction>? actions = null);
}
