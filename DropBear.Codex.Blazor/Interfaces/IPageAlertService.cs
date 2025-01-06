#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Interface for the Page Alert Service.
/// </summary>
public interface IPageAlertService
{
    /// <summary>
    ///     Occurs when a new alert should be shown.
    ///     The delegate returns a <see cref="Task" /> indicating when the alert rendering/processing is complete.
    /// </summary>
    event Func<PageAlertInstance, Task> OnAlert;

    /// <summary>
    ///     Occurs when all alerts should be cleared.
    /// </summary>
    event Action OnClear;

    /// <summary>
    ///     Shows an alert with the specified title, message, and type.
    /// </summary>
    /// <param name="title">Title of the alert.</param>
    /// <param name="message">Message for the alert.</param>
    /// <param name="type">Type of the alert (info, success, etc.).</param>
    /// <param name="duration">
    ///     Time in milliseconds the alert should be displayed.
    ///     If <c>null</c>, a default duration is used.
    /// </param>
    /// <param name="isPermanent">If <c>true</c>, the alert will not automatically disappear.</param>
    void ShowAlert(
        string title,
        string message,
        PageAlertType type = PageAlertType.Info,
        int? duration = 5000,
        bool isPermanent = false);

    /// <summary>
    ///     Shows a success alert with default duration.
    /// </summary>
    /// <param name="title">Title of the alert.</param>
    /// <param name="message">Message for the alert.</param>
    /// <param name="duration">
    ///     Time in milliseconds the alert should be displayed.
    ///     If <c>null</c>, a default duration is used.
    /// </param>
    void ShowSuccess(string title, string message, int? duration = 5000);

    /// <summary>
    ///     Shows an error alert with default duration.
    /// </summary>
    /// <param name="title">Title of the alert.</param>
    /// <param name="message">Message for the alert.</param>
    /// <param name="duration">
    ///     Time in milliseconds the alert should be displayed.
    ///     If <c>null</c>, a default duration is used.
    /// </param>
    void ShowError(string title, string message, int? duration = 8000);

    /// <summary>
    ///     Shows a warning alert. Can be made permanent by setting <paramref name="isPermanent" /> to <c>true</c>.
    /// </summary>
    /// <param name="title">Title of the alert.</param>
    /// <param name="message">Message for the alert.</param>
    /// <param name="isPermanent">If <c>true</c>, the alert will not automatically disappear.</param>
    void ShowWarning(string title, string message, bool isPermanent = false);

    /// <summary>
    ///     Shows an info alert. Can be made permanent by setting <paramref name="isPermanent" /> to <c>true</c>.
    /// </summary>
    /// <param name="title">Title of the alert.</param>
    /// <param name="message">Message for the alert.</param>
    /// <param name="isPermanent">If <c>true</c>, the alert will not automatically disappear.</param>
    void ShowInfo(string title, string message, bool isPermanent = false);

    /// <summary>
    ///     Clears all alerts.
    /// </summary>
    void Clear();
}
