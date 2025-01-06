#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Provides methods to show various page alerts (Success, Error, Warning, etc.)
///     and to clear all alerts.
/// </summary>
public sealed class PageAlertService : IPageAlertService
{
    /// <summary>
    ///     Event fired when a new alert should be shown.
    ///     The handler can perform asynchronous UI rendering or logging.
    /// </summary>
    public event Func<PageAlertInstance, Task>? OnAlert;

    /// <summary>
    ///     Event fired when all alerts should be cleared.
    /// </summary>
    public event Action? OnClear;

    /// <summary>
    ///     Displays a general alert with the specified parameters.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="type">Type of the alert (e.g., Info, Success).</param>
    /// <param name="duration">How long (in milliseconds) the alert is shown. Null means default duration.</param>
    /// <param name="isPermanent">If true, the alert does not automatically disappear.</param>
    public void ShowAlert(
        string title,
        string message,
        PageAlertType type = PageAlertType.Info,
        int? duration = 5000,
        bool isPermanent = false)
    {
        // Create a unique ID for this alert
        var alert = new PageAlertInstance
        {
            Id = Guid.NewGuid().ToString("N"), // Using "N" to remove dashes
            Title = title,
            Message = message,
            Type = type,
            Duration = duration,
            IsPermanent = isPermanent
        };

        OnAlert?.Invoke(alert);
    }

    /// <summary>
    ///     Displays a success alert with an optional duration.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="duration">Optional duration in milliseconds.</param>
    public void ShowSuccess(string title, string message, int? duration = 5000)
    {
        ShowAlert(title, message, PageAlertType.Success, duration);
    }

    /// <summary>
    ///     Displays an error alert with an optional duration.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="duration">Optional duration in milliseconds (default is 8000).</param>
    public void ShowError(string title, string message, int? duration = 8000)
    {
        ShowAlert(title, message, PageAlertType.Error, duration);
    }

    /// <summary>
    ///     Displays a warning alert. Can be made permanent by setting <paramref name="isPermanent" /> to true.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="isPermanent">If true, the alert is not automatically dismissed.</param>
    public void ShowWarning(string title, string message, bool isPermanent = false)
    {
        ShowAlert(title, message, PageAlertType.Warning, null, isPermanent);
    }

    /// <summary>
    ///     Displays an informational alert. Can be made permanent by setting <paramref name="isPermanent" /> to true.
    /// </summary>
    /// <param name="title">The alert's title.</param>
    /// <param name="message">The alert's message body.</param>
    /// <param name="isPermanent">If true, the alert is not automatically dismissed.</param>
    public void ShowInfo(string title, string message, bool isPermanent = false)
    {
        ShowAlert(title, message, PageAlertType.Info, null, isPermanent);
    }

    /// <summary>
    ///     Clears all displayed alerts by invoking the <see cref="OnClear" /> event.
    /// </summary>
    public void Clear()
    {
        OnClear?.Invoke();
    }
}
