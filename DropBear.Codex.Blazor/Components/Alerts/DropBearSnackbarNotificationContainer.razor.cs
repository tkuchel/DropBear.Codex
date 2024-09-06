#region

using DropBear.Codex.Blazor.Arguments.Events;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A container component for displaying snackbar notifications.
/// </summary>
public sealed partial class DropBearSnackbarNotificationContainer : DropBearComponentBase, IAsyncDisposable
{
    private readonly List<SnackbarInstance> _snackbars = new();

    [Inject] private ISnackbarNotificationService SnackbarService { get; set; } = null!;

    /// <summary>
    ///     Disposes of the snackbar notification container asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
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
            catch (JSDisconnectedException)
            {
                // The circuit is disconnected, so we can't invoke JavaScript.
                // This is fine, as the snackbars will be removed when the page unloads.
            }
        }
    }

    /// <summary>
    ///     Initializes the snackbar notification container.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeSnackbarServiceEvents();
    }

    private void SubscribeSnackbarServiceEvents()
    {
        SnackbarService.OnShow += ShowSnackbarAsync;
        SnackbarService.OnHideAll += HideAllSnackbars;
    }

    private void UnsubscribeSnackbarServiceEvents()
    {
        SnackbarService.OnShow -= ShowSnackbarAsync;
        SnackbarService.OnHideAll -= HideAllSnackbars;
    }

    private async void ShowSnackbarAsync(object? sender, SnackbarNotificationEventArgs e)
    {
        var options = e.Options;

        var snackbar = new SnackbarInstance(options);
        _snackbars.Add(snackbar);

        await InvokeAsync(StateHasChanged);
        await Task.Delay(50); // Small delay to ensure the DOM is updated

        if (snackbar.ComponentRef != null)
        {
            await snackbar.ComponentRef.ShowAsync();
        }

        if (options.Duration > 0)
        {
            await Task.Delay(options.Duration);
            await RemoveSnackbarAsync(snackbar);
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
                }
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error removing snackbar: {ex.Message}");
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    private void HideAllSnackbars(object? sender, EventArgs e)
    {
        _ = InvokeAsync(async () =>
        {
            try
            {
                await HideAllSnackbarsInternalAsync();
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error hiding all snackbars: {ex.Message}");
            }
        });
    }

    private async Task HideAllSnackbarsInternalAsync()
    {
        var tasks = _snackbars.Select(s => s.ComponentRef?.DismissAsync() ?? Task.CompletedTask);
        await Task.WhenAll(tasks);
        _snackbars.Clear();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSnackbarAction(SnackbarInstance snackbar)
    {
        try
        {
            await snackbar.OnAction.Invoke();

            await RemoveSnackbarAsync(snackbar);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error handling snackbar action: {ex.Message}");
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
