#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service to manage page alerts.
/// </summary>
public sealed class PageAlertService : IPageAlertService
{
    private readonly List<PageAlert> _alerts = new(); // Backing field for managing alerts.

    public IReadOnlyList<PageAlert> Alerts =>
        _alerts.AsReadOnly(); // Exposes alerts as read-only to ensure encapsulation.

    public event EventHandler<EventArgs>? OnChange; // Event to notify the UI when alerts are updated.

    /// <summary>
    ///     Removes an alert by its ID.
    /// </summary>
    /// <param name="id">The ID of the alert to remove.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result RemoveAlert(Guid id)
    {
        var alert = _alerts.Find(a => a.Id == id);
        if (alert is not { IsDismissible: true })
        {
            return Result.Failure("Alert is not dismissible or does not exist.");
        }

        _alerts.RemoveAll(a => a.Id == id);
        NotifyStateChanged();
        return Result.Success();
    }

    /// <summary>
    ///     Clears all alerts from the list.
    /// </summary>
    /// <returns>A result indicating the success of the operation.</returns>
    public Result ClearAlerts()
    {
        _alerts.Clear();
        NotifyStateChanged();
        return Result.Success();
    }

    /// <summary>
    ///     Adds an alert to the list with optional duration.
    /// </summary>
    /// <param name="title">The title of the alert.</param>
    /// <param name="message">The message of the alert.</param>
    /// <param name="type">The type of the alert.</param>
    /// <param name="isDismissible">Indicates if the alert can be dismissed.</param>
    /// <param name="durationMs">Optional duration in milliseconds before the alert is removed automatically.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result AddAlert(string title, string message, AlertType type, bool isDismissible, int? durationMs = 5000)
    {
        var alert = new PageAlert(title, message, type, isDismissible);
        _alerts.Add(alert);
        NotifyStateChanged();

        if (durationMs.HasValue)
        {
            // Removing the alert after a delay if a duration is specified.
            _ = RemoveAlertAfterDelay(alert.Id, durationMs.Value);
        }

        return Result.Success();
    }

    /// <summary>
    ///     Asynchronously adds an alert to the list.
    /// </summary>
    /// <param name="title">The title of the alert.</param>
    /// <param name="message">The message of the alert.</param>
    /// <param name="type">The type of the alert.</param>
    /// <param name="isDismissible">Indicates if the alert can be dismissed.</param>
    /// <param name="durationMs">Optional duration in milliseconds before the alert is removed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<Result> AddAlertAsync(string title, string message, AlertType type, bool isDismissible,
        int? durationMs = 5000)
    {
        return await Task.Run(() => AddAlert(title, message, type, isDismissible, durationMs)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Asynchronously removes an alert by its ID.
    /// </summary>
    /// <param name="id">The ID of the alert to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<Result> RemoveAlertAsync(Guid id)
    {
        return await Task.Run(() => RemoveAlert(id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes an alert after a specified delay.
    /// </summary>
    /// <param name="id">The ID of the alert to remove.</param>
    /// <param name="delayMs">The delay in milliseconds before removing the alert.</param>
    private async Task RemoveAlertAfterDelay(Guid id, int delayMs)
    {
        await Task.Delay(delayMs).ConfigureAwait(false);
        await RemoveAlertAsync(id).ConfigureAwait(false);
    }

    /// <summary>
    ///     Notifies subscribers that the alerts have been updated.
    /// </summary>
    private void NotifyStateChanged()
    {
        try
        {
            OnChange?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while notifying state change. Error: {ErrorMessage}", ex.Message);
        }
    }
}
