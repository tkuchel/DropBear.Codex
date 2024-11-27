#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public sealed partial class DropBearSnackbar : DropBearComponentBase
{
    private bool _isDisposed;

    [Parameter] public SnackbarInstance SnackbarInstance { get; set; } = null!;
    [Parameter] public EventCallback OnClose { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await SafeJsVoidInteropAsync("DropBearSnackbar.initialize", SnackbarInstance.Id);
                await SafeJsVoidInteropAsync("DropBearSnackbar.show", SnackbarInstance.Id);

                if (SnackbarInstance is { RequiresManualClose: false, Duration: > 0 })
                {
                    await SafeJsVoidInteropAsync("DropBearSnackbar.startProgress",
                        SnackbarInstance.Id, SnackbarInstance.Duration);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing snackbar");
            }
        }
    }

    private string GetCssClasses()
    {
        return $"dropbear-snackbar {SnackbarInstance.Type.ToString().ToLower()}";
    }

    private string GetIcon()
    {
        return SnackbarInstance.Type switch
        {
            SnackbarType.Success =>
                @"<svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
        <path d='M20 6L9 17l-5-5'/>
    </svg>",

            SnackbarType.Error =>
                @"<svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
        <circle cx='12' cy='12' r='10'/>
        <path d='M12 8v4m0 4h.01'/>
    </svg>",

            SnackbarType.Warning =>
                @"<svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
        <path d='M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0zM12 9v4m0 4h.01'/>
    </svg>",

            _ => @"<svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
        <circle cx='12' cy='12' r='10'/>
        <path d='M12 16v-4m0-4h.01'/>
    </svg>"
        };
    }

    private async Task HandleActionClick(SnackbarAction action)
    {
        try
        {
            if (action.OnClick is not null)
            {
                await action.OnClick.Invoke();
            }

            await Close();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling action click");
        }
    }

    private async Task Close()
    {
        try
        {
            await SafeJsVoidInteropAsync("DropBearSnackbar.hide", SnackbarInstance.Id);
            await Task.Delay(300); // Match JS animation duration
            await OnClose.InvokeAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error closing snackbar");
        }
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                await SafeJsVoidInteropAsync("DropBearSnackbar.dispose", SnackbarInstance.Id);
            }

            _isDisposed = true;
            await base.DisposeAsync(disposing);
        }
    }
}
