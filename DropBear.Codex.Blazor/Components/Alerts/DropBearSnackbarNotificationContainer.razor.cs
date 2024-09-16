#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Messaging.Models;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using MessagePipe;
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
    private IDisposable? _hideAllSubscription;
    private IDisposable? _showSubscription;

    [Inject] private ISubscriber<ShowSnackbarMessage> ShowSnackbarSubscriber { get; set; } = null!;
    [Inject] private ISubscriber<HideAllSnackbarsMessage> HideAllSnackbarsSubscriber { get; set; } = null!;

    /// <summary>
    ///     Disposes of the snackbar notification container asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Logger.Debug("Disposing SnackbarNotificationContainer...");

        _showSubscription?.Dispose();
        _hideAllSubscription?.Dispose();

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
        SubscribeToMessages();
        Logger.Debug("SnackbarNotificationContainer initialized.");
    }

    private void SubscribeToMessages()
    {
        _showSubscription = ShowSnackbarSubscriber.Subscribe(ShowSnackbarAsync);
        _hideAllSubscription = HideAllSnackbarsSubscriber.Subscribe(_ => HideAllSnackbarsAsync());
        Logger.Debug("Subscribed to MessagePipe messages.");
    }

    private async void ShowSnackbarAsync(ShowSnackbarMessage message)
    {
        var options = message.Options;

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
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HideAllSnackbarsAsync()
    {
        Logger.Debug("Hiding all snackbars...");

        try
        {
            var tasks = _snackbars.Select(s => s.ComponentRef?.DismissAsync() ?? Task.CompletedTask);
            await Task.WhenAll(tasks);
            _snackbars.Clear();

            Logger.Debug("All snackbars hidden and cleared.");
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while hiding all snackbars.");
        }
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
