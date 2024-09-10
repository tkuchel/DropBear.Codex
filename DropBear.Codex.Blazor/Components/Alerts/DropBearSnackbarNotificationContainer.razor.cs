#region

using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A container component for displaying snackbar notifications.
/// </summary>
public sealed partial class DropBearSnackbarNotificationContainer : DropBearComponentBase, IAsyncDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearSnackbarNotificationContainer>();
    private readonly List<SnackbarInstance> _snackbars = new();

    [Inject] private ISnackbarNotificationService SnackbarService { get; set; } = null!;

    /// <summary>
    ///     Disposes of the snackbar notification container asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Logger.Debug("Disposing SnackbarNotificationContainer...");

        UnsubscribeSnackbarServiceEvents();

        foreach (var snackbar in _snackbars)
        {
            try
            {
                if (snackbar.ComponentRef is not null)
                {
                    await snackbar.ComponentRef.DisposeAsync();
                }
            }
            catch (JSDisconnectedException ex)
            {
                Logger.Warning(ex, "JSDisconnectedException occurred while disposing snackbar: {SnackbarId}",
                    snackbar.Id);
                // The circuit is disconnected, so we can't invoke JavaScript.
                // Snackbars will be removed when the page unloads.
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected error occurred while disposing snackbar: {SnackbarId}", snackbar.Id);
            }
        }

        Logger.Debug("SnackbarNotificationContainer disposed.");
    }

    /// <summary>
    ///     Initializes the snackbar notification container.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeSnackbarServiceEvents();
        Logger.Debug("SnackbarNotificationContainer initialized.");
    }

    private void SubscribeSnackbarServiceEvents()
    {
        SnackbarService.OnShow += ShowSnackbarAsync;
        SnackbarService.OnHideAll += HideAllSnackbars;
        Logger.Debug("Subscribed to SnackbarService events.");
    }

    private void UnsubscribeSnackbarServiceEvents()
    {
        SnackbarService.OnShow -= ShowSnackbarAsync;
        SnackbarService.OnHideAll -= HideAllSnackbars;
        Logger.Debug("Unsubscribed from SnackbarService events.");
    }

    private async Task ShowSnackbarAsync(object? sender, SnackbarNotificationEventArgs e)
    {
        var options = e.Options;

        var snackbar = new SnackbarInstance(options);
        _snackbars.Add(snackbar);
        Logger.Debug("Snackbar added with ID: {SnackbarId}", snackbar.Id);

        await InvokeAsync(StateHasChanged);
        await Task.Delay(50); // Small delay to ensure the DOM is updated

        try
        {
            if (snackbar.ComponentRef is not null)
            {
                await snackbar.ComponentRef.ShowAsync();
                Logger.Debug("Snackbar shown with ID: {SnackbarId}", snackbar.Id);
            }

            if (options.Duration > 0)
            {
                await Task.Delay(options.Duration);
                await RemoveSnackbarAsync(snackbar);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while showing snackbar: {SnackbarId}", snackbar.Id);
            throw;
        }
    }

    private async Task RemoveSnackbarAsync(SnackbarInstance snackbar)
    {
        if (_snackbars.Contains(snackbar))
        {
            _snackbars.Remove(snackbar);

            try
            {
                if (snackbar.ComponentRef is not null)
                {
                    await snackbar.ComponentRef.DismissAsync();
                    Logger.Debug("Snackbar dismissed with ID: {SnackbarId}", snackbar.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurred while removing snackbar: {SnackbarId}", snackbar.Id);
                throw;
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HideAllSnackbars(object? sender, EventArgs e)
    {
        Logger.Debug("Hiding all snackbars...");

        try
        {
            await HideAllSnackbarsInternalAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while hiding all snackbars.");
            throw;
        }
    }

    private async Task HideAllSnackbarsInternalAsync()
    {
        var tasks = _snackbars.Select(s => s.ComponentRef?.DismissAsync() ?? Task.CompletedTask);
        await Task.WhenAll(tasks);
        _snackbars.Clear();

        Logger.Debug("All snackbars hidden and cleared.");
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSnackbarAction(SnackbarInstance snackbar)
    {
        try
        {
            await snackbar.OnAction.Invoke();
            Logger.Debug("Snackbar action invoked for SnackbarId: {SnackbarId}", snackbar.Id);

            await RemoveSnackbarAsync(snackbar);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while handling snackbar action: {SnackbarId}", snackbar.Id);
            throw;
        }
    }

    private sealed class SnackbarInstance : SnackbarNotificationOptions
    {
        public SnackbarInstance(SnackbarNotificationOptions options)
            : base(options.Title, options.Message, options.Type, options.Duration, options.IsDismissible,
                options.ActionText, options.OnAction)
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }
        public DropBearSnackbarNotification? ComponentRef { get; set; }
    }
}
