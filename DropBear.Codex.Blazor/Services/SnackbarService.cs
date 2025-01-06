#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Provides methods to show snackbar notifications and manage their lifecycle.
/// </summary>
public class SnackbarService : ISnackbarService
{
    private readonly ConcurrentBag<SnackbarInstance> _activeSnackbars = [];
    private readonly ILogger _logger;

    /// <summary>
    ///     Creates a new instance of the <see cref="SnackbarService" /> class.
    /// </summary>
    public SnackbarService()
    {
        _logger = LoggerFactory.Logger.ForContext<SnackbarService>();
    }

    /// <summary>
    ///     Event fired when a snackbar should be shown.
    ///     The handler is responsible for rendering or displaying the snackbar in the UI.
    /// </summary>
    public event Func<SnackbarInstance, Task>? OnShow;

    /// <summary>
    ///     Shows a specific snackbar instance.
    /// </summary>
    /// <param name="snackbar">The <see cref="SnackbarInstance" /> to be shown.</param>
    /// <returns>A <see cref="Result{T, TError}" /> indicating success or failure.</returns>
    public async Task<Result<Unit, SnackbarError>> Show(SnackbarInstance snackbar)
    {
        if (OnShow is null)
        {
            // No registered container to handle showing the snackbar
            return Result<Unit, SnackbarError>.Failure(
                new SnackbarError("No snackbar container is registered"));
        }

        try
        {
            _activeSnackbars.Add(snackbar);
            // Fire the event so UI can show the snackbar
            await OnShow.Invoke(snackbar).ConfigureAwait(false);
            return Result<Unit, SnackbarError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to show snackbar");
            return Result<Unit, SnackbarError>.Failure(
                new SnackbarError($"Failed to show snackbar: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Shows a success-type snackbar.
    /// </summary>
    /// <param name="title">Title for the snackbar.</param>
    /// <param name="message">Message to display.</param>
    /// <param name="duration">How long (in milliseconds) before auto-close.</param>
    /// <param name="actions">Optional list of actions for user interaction.</param>
    /// <returns>A <see cref="Task" /> representing the async operation.</returns>
    public async Task<Result<Unit, SnackbarError>> ShowSuccess(
        string title,
        string message,
        int duration = 5000,
        List<SnackbarAction>? actions = null)
    {
        return await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Success,
            Duration = duration,
            Actions = actions ?? new List<SnackbarAction>()
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows an error-type snackbar.
    ///     If <paramref name="duration" /> is 0, it requires manual close.
    /// </summary>
    /// <param name="title">Title for the snackbar.</param>
    /// <param name="message">Message to display.</param>
    /// <param name="duration">How long (in milliseconds) before auto-close; if 0, manual close is required.</param>
    /// <param name="actions">Optional list of actions for user interaction.</param>
    /// <returns>A <see cref="Task" /> representing the async operation.</returns>
    public async Task<Result<Unit, SnackbarError>> ShowError(
        string title,
        string message,
        int duration = 0,
        List<SnackbarAction>? actions = null)
    {
        return await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Error,
            Duration = duration,
            RequiresManualClose = duration == 0,
            Actions = actions ?? new List<SnackbarAction>()
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows a warning-type snackbar.
    /// </summary>
    /// <param name="title">Title for the snackbar.</param>
    /// <param name="message">Message to display.</param>
    /// <param name="duration">How long (in milliseconds) before auto-close.</param>
    /// <param name="actions">Optional list of actions for user interaction.</param>
    /// <returns>A <see cref="Task" /> representing the async operation.</returns>
    public async Task<Result<Unit, SnackbarError>> ShowWarning(
        string title,
        string message,
        int duration = 8000,
        List<SnackbarAction>? actions = null)
    {
        return await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Warning,
            Duration = duration,
            Actions = actions ?? new List<SnackbarAction>()
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows an informational snackbar.
    /// </summary>
    /// <param name="title">Title for the snackbar.</param>
    /// <param name="message">Message to display.</param>
    /// <param name="duration">How long (in milliseconds) before auto-close.</param>
    /// <param name="actions">Optional list of actions for user interaction.</param>
    /// <returns>A <see cref="Task" /> representing the async operation.</returns>
    public async Task<Result<Unit, SnackbarError>> ShowInformation(
        string title,
        string message,
        int duration = 5000,
        List<SnackbarAction>? actions = null)
    {
        return await Show(new SnackbarInstance
        {
            Title = title,
            Message = message,
            Type = SnackbarType.Information,
            Duration = duration,
            Actions = actions ?? new List<SnackbarAction>()
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Disposes this service by clearing active snackbars.
    /// </summary>
    public void Dispose()
    {
        // Clear any active snackbars
        _activeSnackbars.Clear();
        GC.SuppressFinalize(this);
    }
}
