#region

using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service to manage page alerts.
/// </summary>
public sealed class PageAlertService : IPageAlertService
{
    private readonly List<PageAlert> _alerts = new();
    public IReadOnlyList<PageAlert> Alerts => _alerts.AsReadOnly();

    public event EventHandler<EventArgs>? OnChange; // Event to notify the UI that the alerts have changed

    /// <summary>
    ///     Removes an alert by its ID.
    /// </summary>
    /// <param name="id">The ID of the alert to remove.</param>
    /// <returns>A result indicating the success or failure of the operation.</returns>
    public Result RemoveAlert(Guid id)
    {
        var alert = _alerts.Find(a => a.Id == id);
        if (alert is not { IsDismissible: true })
        {
            return Result.Failure("Alert is not dismissible or does not exist.");
        }

        _alerts.RemoveAll(a => a.Id == id);
        OnChange?.Invoke(this, EventArgs.Empty);
        return Result.Success();
    }

    /// <summary>
    ///     Clears all alerts from the list.
    /// </summary>
    /// <returns>A result indicating the success of the operation.</returns>
    public Result ClearAlerts()
    {
        _alerts.Clear();
        OnChange?.Invoke(this, EventArgs.Empty);
        return Result.Success();
    }

    /// <summary>
    ///     Adds an alert to the list.
    /// </summary>
    /// <param name="title">The title of the alert.</param>
    /// <param name="message">The message of the alert.</param>
    /// <param name="type">The type of the alert.</param>
    /// <param name="isDismissible">Indicates whether the alert is dismissible.</param>
    /// <param name="durationMs">The duration in milliseconds for the alert to be displayed.</param>
    /// <returns>A result indicating the success or failure of the operation.</returns>
    public Result AddAlert(string title, string message, AlertType type, bool isDismissible, int? durationMs = 5000)
    {
        var alert = new PageAlert(title, message, type, isDismissible);
        _alerts.Add(alert);
        OnChange?.Invoke(this, EventArgs.Empty);

        if (durationMs.HasValue)
        {
            _ = RemoveAlertAfterDelay(alert.Id, durationMs.Value);
        }

        return Result.Success();
    }

    /// <summary>
    ///     Adds an alert to the list asynchronously.
    /// </summary>
    /// <param name="title">The title of the alert.</param>
    /// <param name="message">The message of the alert.</param>
    /// <param name="type">The type of the alert.</param>
    /// <param name="isDismissible">Indicates whether the alert is dismissible.</param>
    /// <param name="durationMs">The duration in milliseconds for the alert to be displayed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<Result> AddAlertAsync(string title, string message, AlertType type, bool isDismissible,
        int? durationMs = 5000)
    {
        var result = await Task.Run(() => AddAlert(title, message, type, isDismissible, durationMs))
            .ConfigureAwait(false);
        return result;
    }

    /// <summary>
    ///     Removes an alert by its ID asynchronously.
    /// </summary>
    /// <param name="id">The ID of the alert to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task<Result> RemoveAlertAsync(Guid id)
    {
        var result = await Task.Run(() => RemoveAlert(id)).ConfigureAwait(false);
        return result;
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
}
