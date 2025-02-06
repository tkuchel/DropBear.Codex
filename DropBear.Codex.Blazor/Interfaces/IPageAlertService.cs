#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;

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
    Task ShowAlertAsync(string title, string message, PageAlertType type = PageAlertType.Info,
        int? duration = null, bool isPermanent = false);

    /// <summary>
    ///     Shows a success alert.
    /// </summary>
    Task ShowSuccessAsync(string title, string message, int? duration = 5000);

    /// <summary>
    ///     Shows an error alert.
    /// </summary>
    Task ShowErrorAsync(string title, string message, int? duration = 8000);

    /// <summary>
    ///     Shows a warning alert.
    /// </summary>
    Task ShowWarningAsync(string title, string message, bool isPermanent = false);

    /// <summary>
    ///     Shows an info alert.
    /// </summary>
    Task ShowInfoAsync(string title, string message, bool isPermanent = false);

    /// <summary>
    ///     Clears all displayed alerts.
    /// </summary>
    Task ClearAsync();

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
