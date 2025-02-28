#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Defines methods for displaying and managing page-level alerts.
/// </summary>
public interface IPageAlertService : IAsyncDisposable
{
    /// <summary>
    ///     Event fired when a new alert should be shown.
    /// </summary>
    event Func<PageAlertInstance, Task>? OnAlert;

    /// <summary>
    ///     Event fired when all alerts should be cleared.
    /// </summary>
    event Action? OnClear;

    /// <summary>
    ///     Shows a general alert with the specified parameters.
    /// </summary>
    /// <param name="title">The alert title.</param>
    /// <param name="message">The alert message content.</param>
    /// <param name="type">The alert type (Success, Error, Warning, Info).</param>
    /// <param name="duration">Optional duration in milliseconds before auto-dismiss.</param>
    /// <param name="isPermanent">Whether the alert requires manual dismissal.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    Task<Result<Unit, AlertError>> ShowAlertAsync(string title, string message, PageAlertType type = PageAlertType.Info,
        int? duration = null, bool isPermanent = false);

    /// <summary>
    ///     Shows a success alert.
    /// </summary>
    /// <param name="title">The alert title.</param>
    /// <param name="message">The alert message content.</param>
    /// <param name="duration">Optional duration in milliseconds before auto-dismiss.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    Task<Result<Unit, AlertError>> ShowSuccessAsync(string title, string message, int? duration = 5000);

    /// <summary>
    ///     Shows an error alert.
    /// </summary>
    /// <param name="title">The alert title.</param>
    /// <param name="message">The alert message content.</param>
    /// <param name="duration">Optional duration in milliseconds before auto-dismiss.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    Task<Result<Unit, AlertError>> ShowErrorAsync(string title, string message, int? duration = 8000);

    /// <summary>
    ///     Shows a warning alert.
    /// </summary>
    /// <param name="title">The alert title.</param>
    /// <param name="message">The alert message content.</param>
    /// <param name="isPermanent">Whether the alert requires manual dismissal.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    Task<Result<Unit, AlertError>> ShowWarningAsync(string title, string message, bool isPermanent = false);

    /// <summary>
    ///     Shows an info alert.
    /// </summary>
    /// <param name="title">The alert title.</param>
    /// <param name="message">The alert message content.</param>
    /// <param name="isPermanent">Whether the alert requires manual dismissal.</param>
    /// <returns>A result indicating success or failure of the operation.</returns>
    Task<Result<Unit, AlertError>> ShowInfoAsync(string title, string message, bool isPermanent = false);

    /// <summary>
    ///     Clears all displayed alerts.
    /// </summary>
    /// <returns>A result indicating success or failure of the operation.</returns>
    Task<Result<Unit, AlertError>> ClearAsync();

    #region Backwards Compatibility Methods

    /// <summary>
    ///     Shows a general alert (legacy sync method).
    /// </summary>
    void ShowAlert(string title, string message, PageAlertType type = PageAlertType.Info,
        int? duration = null, bool isPermanent = false);

    /// <summary>
    ///     Shows a success alert (legacy sync method).
    /// </summary>
    void ShowSuccess(string title, string message, int? duration = 5000);

    /// <summary>
    ///     Shows an error alert (legacy sync method).
    /// </summary>
    void ShowError(string title, string message, int? duration = 8000);

    /// <summary>
    ///     Shows a warning alert (legacy sync method).
    /// </summary>
    void ShowWarning(string title, string message, bool isPermanent = false);

    /// <summary>
    ///     Shows an info alert (legacy sync method).
    /// </summary>
    void ShowInfo(string title, string message, bool isPermanent = false);

    /// <summary>
    ///     Clears all displayed alerts (legacy sync method).
    /// </summary>
    void Clear();

    #endregion
}
