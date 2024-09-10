#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service to manage page alerts.
/// </summary>
public sealed class PageAlertService : IPageAlertService
{
    // Logger for PageAlertService
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<PageAlertService>();

    // Backing field for managing alerts.
    private readonly List<PageAlert> _alerts = new();

    /// <summary>
    ///     Exposes alerts as read-only to ensure encapsulation.
    /// </summary>
    public IReadOnlyList<PageAlert> Alerts => _alerts.AsReadOnly();

    // Event to notify the UI when alerts are updated.
    public event EventHandler<EventArgs>? OnChange;

    /// <summary>
    ///     Removes an alert by its ID.
    /// </summary>
    /// <param name="id">The ID of the alert to remove.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result RemoveAlert(Guid id)
    {
        try
        {
            var alert = _alerts.Find(a => a.Id == id);

            if (alert is not { IsDismissible: true })
            {
                Logger.Warning("Attempted to remove non-dismissible or non-existing alert with ID: {AlertId}", id);
                return Result.Failure("Alert is not dismissible or does not exist.");
            }

            _alerts.RemoveAll(a => a.Id == id);
            Logger.Information("Alert with ID {AlertId} removed successfully.", id);
            NotifyStateChanged();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while removing alert with ID {AlertId}.", id);
            return Result.Failure("An error occurred while removing the alert.");
        }
    }

    /// <summary>
    ///     Clears all alerts from the list.
    /// </summary>
    /// <returns>A result indicating the success of the operation.</returns>
    public Result ClearAlerts()
    {
        try
        {
            _alerts.Clear();
            Logger.Information("All alerts cleared successfully.");
            NotifyStateChanged();
            return Result.Success();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while clearing all alerts.");
            return Result.Failure("An error occurred while clearing alerts.");
        }
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
        try
        {
            var alert = new PageAlert(title, message, type, isDismissible);
            _alerts.Add(alert);
            Logger.Information("Added new alert: {AlertTitle} - Type: {AlertType}, Dismissible: {IsDismissible}", title, type, isDismissible);

            NotifyStateChanged();

            if (durationMs.HasValue)
            {
                // Removing the alert after a delay if a duration is specified.
                Logger.Debug("Scheduling removal of alert {AlertId} after {Duration}ms", alert.Id, durationMs.Value);
                _ = RemoveAlertAfterDelay(alert.Id, durationMs.Value);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while adding a new alert: {AlertTitle}", title);
            return Result.Failure("An error occurred while adding the alert.");
        }
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
    public async Task<Result> AddAlertAsync(string title, string message, AlertType type, bool isDismissible, int? durationMs = 5000)
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
        try
        {
            Logger.Debug("Starting delay of {Delay}ms for alert {AlertId} removal.", delayMs, id);
            await Task.Delay(delayMs).ConfigureAwait(false);
            await RemoveAlertAsync(id).ConfigureAwait(false);
            Logger.Information("Alert {AlertId} removed after delay.", id);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while removing alert {AlertId} after delay.", id);
        }
    }

    /// <summary>
    ///     Notifies subscribers that the alerts have been updated.
    /// </summary>
    private void NotifyStateChanged()
    {
        try
        {
            OnChange?.Invoke(this, EventArgs.Empty);
            Logger.Debug("PageAlertService state changed, notifying subscribers.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while notifying state change.");
        }
    }
}
