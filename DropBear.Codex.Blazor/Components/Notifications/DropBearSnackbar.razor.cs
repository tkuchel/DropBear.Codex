#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Components.Menus;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

/// <summary>
///     Represents a single snackbar notification with optional actions and auto-dismiss behavior.
/// </summary>
public sealed partial class DropBearSnackbar : DropBearComponentBase
{
    private bool _isDisposed;
    private ElementReference _snackbarRef;
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearSnackbar>();
    /// <summary>
    ///     Holds information about the snackbar instance: title, message, duration, actions, etc.
    /// </summary>
    [Parameter]
    public SnackbarInstance SnackbarInstance { get; set; } = null!;

    /// <summary>
    ///     An optional callback invoked when the snackbar is closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    ///     Additional HTML attributes for the root container.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                // Initialize the snackbar in JS for positioning and animation
                await SafeJsVoidInteropAsync("DropBearSnackbar.initialize", SnackbarInstance.Id);

                // Show the snackbar
                await SafeJsVoidInteropAsync("DropBearSnackbar.show", SnackbarInstance.Id);

                // If it's auto-dismiss and has a positive duration, start progress
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

    /// <summary>
    ///     Builds the CSS class string for the snackbar based on its type.
    /// </summary>
    private string GetCssClasses()
    {
        // e.g., "dropbear-snackbar success"
        var cssClass = $"dropbear-snackbar {SnackbarInstance.Type.ToString().ToLower()}";
        return cssClass;
    }

    /// <summary>
    ///     Returns an SVG icon as a string, matching the snackbar's type.
    /// </summary>
    private string GetIcon()
    {
        return SnackbarInstance.Type switch
        {
            SnackbarType.Success => @"
                <svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
                    <path d='M20 6L9 17l-5-5'/>
                </svg>",
            SnackbarType.Error => @"
                <svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
                    <circle cx='12' cy='12' r='10'/>
                    <path d='M12 8v4m0 4h.01'/>
                </svg>",
            SnackbarType.Warning => @"
                <svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
                    <path d='M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0zM12 9v4m0 4h.01'/>
                </svg>",
            _ => @"
                <svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
                    <circle cx='12' cy='12' r='10'/>
                    <path d='M12 16v-4m0-4h.01'/>
                </svg>"
        };
    }

    /// <summary>
    ///     Handles a click on any action button and closes the snackbar afterward.
    /// </summary>
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

    /// <summary>
    ///     Closes the snackbar (hides it in JS) and then invokes OnClose callback.
    /// </summary>
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

    /// <inheritdoc />
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                try
                {
                    // Tell JS to dispose resources for this snackbar (listeners, etc.)
                    await SafeJsVoidInteropAsync("DropBearSnackbar.dispose", SnackbarInstance.Id);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error disposing snackbar in JS");
                }
            }

            _isDisposed = true;
            await base.DisposeAsync(disposing);
        }
    }
}
