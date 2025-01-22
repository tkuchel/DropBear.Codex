#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Tasks.TaskExecutionEngine.Models;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

/// <summary>
///     Represents a single snackbar notification with optional actions and auto-dismiss behavior.
/// </summary>
public sealed partial class DropBearSnackbar : DropBearComponentBase
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearSnackbar>();
    private static readonly int MAX_RETRIES = 3;
    private static readonly int RETRY_DELAY_MS = 500;
    private readonly CancellationTokenSource _disposalTokenSource = new();
    private readonly AsyncLock _initializationLock = new();
    private bool _isDisposed;
    private ElementReference _snackbarRef;

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
        // Call the base class's OnAfterRenderAsync
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
        {
            return;
        }

        // Use the async lock to avoid concurrent calls
        using (await _initializationLock.LockAsync(_disposalTokenSource.Token))
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                // 1) Ensure the DropBearSnackbar module is fully registered
                await EnsureJsModuleInitializedAsync("DropBearSnackbar");

                // 2) Retry logic if you want to handle transient errors
                var retryCount = 0;
                while (retryCount < MAX_RETRIES)
                {
                    try
                    {
                        // 3) Create a snackbar manager for this ID
                        await SafeJsVoidInteropAsync("DropBearSnackbar.createSnackbar", SnackbarInstance.Id);

                        // 4) Show the snackbar
                        await SafeJsVoidInteropAsync("DropBearSnackbar.show", SnackbarInstance.Id);

                        // 5) If auto-dismiss, start progress
                        if (!SnackbarInstance.RequiresManualClose && SnackbarInstance.Duration > 0)
                        {
                            await SafeJsVoidInteropAsync("DropBearSnackbar.startProgress",
                                SnackbarInstance.Id, SnackbarInstance.Duration);
                        }

                        Logger.Debug("JS interop for DropBearSnackbar is initialized and shown.");
                        break; // success
                    }
                    catch (Exception ex) when (retryCount < MAX_RETRIES - 1)
                    {
                        Logger.Warning(ex, "Retry {Count} for snackbar {SnackbarId}", retryCount + 1,
                            SnackbarInstance.Id);
                        await Task.Delay(RETRY_DELAY_MS);
                        retryCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing Blazor JS interop for DropBearSnackbar: {SnackbarId}",
                    SnackbarInstance.Id);
                // optionally re-throw or handle
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
