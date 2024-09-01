#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Interface for a service that manages page alerts.
/// </summary>
public interface IPageAlertService
{
    /// <summary>
    ///     Gets the list of alerts.
    /// </summary>
    IReadOnlyList<PageAlert> Alerts { get; }

    /// <summary>
    ///     Event triggered when the alerts list changes.
    /// </summary>
    event EventHandler<EventArgs> OnChange;

    /// <summary>
    ///     Adds an alert to the list.
    /// </summary>
    /// <param name="title">The title of the alert.</param>
    /// <param name="message">The message of the alert.</param>
    /// <param name="type">The type of the alert.</param>
    /// <param name="isDismissible">Indicates whether the alert is dismissible.</param>
    /// <param name="durationMs">The duration in milliseconds for the alert to be displayed.</param>
    /// <returns>A result indicating the success or failure of the operation.</returns>
    Result AddAlert(string title, string message, AlertType type, bool isDismissible, int? durationMs = 5000);

    /// <summary>
    ///     Adds an alert to the list asynchronously.
    /// </summary>
    /// <param name="title">The title of the alert.</param>
    /// <param name="message">The message of the alert.</param>
    /// <param name="type">The type of the alert.</param>
    /// <param name="isDismissible">Indicates whether the alert is dismissible.</param>
    /// <param name="durationMs">The duration in milliseconds for the alert to be displayed.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and returns a result indicating the success or failure of
    ///     the operation.
    /// </returns>
    Task<Result> AddAlertAsync(string title, string message, AlertType type, bool isDismissible,
        int? durationMs = 5000);

    /// <summary>
    ///     Removes an alert by its ID.
    /// </summary>
    /// <param name="id">The ID of the alert to remove.</param>
    /// <returns>A result indicating the success or failure of the operation.</returns>
    Result RemoveAlert(Guid id);

    /// <summary>
    ///     Removes an alert by its ID asynchronously.
    /// </summary>
    /// <param name="id">The ID of the alert to remove.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation and returns a result indicating the success or failure of
    ///     the operation.
    /// </returns>
    Task<Result> RemoveAlertAsync(Guid id);

    /// <summary>
    ///     Clears all alerts.
    /// </summary>
    /// <returns>A result indicating the success of the operation.</returns>
    Result ClearAlerts();
}
