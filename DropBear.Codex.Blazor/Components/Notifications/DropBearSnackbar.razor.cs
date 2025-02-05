#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

/// <summary>
///     A simplified Blazor component for displaying a snackbar.
/// </summary>
public sealed partial class DropBearSnackbar : DropBearComponentBase
{
    private const string JsModuleName = JsModuleNames.Snackbar;
    private IJSObjectReference? _jsModule;

    /// <summary>
    ///     Unique snackbar instance details (title, message, duration, etc.)
    /// </summary>
    [Parameter]
    [EditorRequired]
    public SnackbarInstance SnackbarInstance { get; init; } = null!;

    /// <summary>
    ///     Invoked when the snackbar closes.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    ///     Additional attributes for the root element.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    /// <summary>
    ///     Convenience property for building CSS classes.
    /// </summary>
    private string CssClasses => $"dropbear-snackbar {SnackbarInstance.Type.ToString().ToLower()}";

    /// <inheritdoc />
    /// <remarks>
    ///     Loads the JS module on first render, then calls a matching "initialize"
    ///     method, passing the unique ID and a .NET reference.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
        {
            return;
        }

        try
        {
            // 1) Retrieve the module reference once
            _jsModule = await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);

            if (_jsModule is not null)
            {
                // 2) Call an "createSnackbar" function in the JS
                await _jsModule.InvokeVoidAsync($"{JsModuleName}API.createSnackbar", SnackbarInstance.Id, SnackbarInstance);

                // 3) Pass the .NET reference to the JS module
                await _jsModule.InvokeVoidAsync($"{JsModuleName}API.setDotNetReference", SnackbarInstance.Id,
                    DotNetObjectReference.Create(this));

                // (Optional) If you want to show immediately:
                await _jsModule.InvokeVoidAsync($"{JsModuleName}API.show", SnackbarInstance.Id);

                // (Optional) If you have auto-close logic in JS, start progress here
                if (SnackbarInstance is { RequiresManualClose: false, Duration: > 0 })
                {
                    await _jsModule.InvokeVoidAsync($"{JsModuleName}API.startProgress",
                        SnackbarInstance.Id,
                        SnackbarInstance.Duration
                    );
                }
            }

            LogDebug("Snackbar {Id} initialized via JS module.", SnackbarInstance.Id);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize DropBearSnackbar {Id}", ex, SnackbarInstance.Id);
        }
    }

    /// <summary>
    ///     (Optional) Provide a method for an action button within the snackbar
    ///     that, when clicked, triggers custom logic or closes the snackbar.
    /// </summary>
    private async Task HandleActionClick(SnackbarAction action)
    {
        if (action.OnClick != null)
        {
            try
            {
                await action.OnClick.Invoke().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError("Error in action OnClick: {Label}", ex, action.Label);
            }
        }

        // Optionally, call a "hide" method in JS or do the .NET close sequence
        await CloseAsync();
    }

    /// <summary>
    ///     .NET method to hide the snackbar, triggered from .NET or JS
    /// </summary>
    private async Task CloseAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        if (_jsModule is null)
        {
            return;
        }

        try
        {
            await _jsModule.InvokeVoidAsync($"{JsModuleName}API.hide", SnackbarInstance.Id);
            if (OnClose.HasDelegate)
            {
                await OnClose.InvokeAsync();
            }

            LogDebug("Snackbar {Id} closed", SnackbarInstance.Id);
        }
        catch (Exception ex)
        {
            LogError("Failed to close DropBearSnackbar {Id}", ex, SnackbarInstance.Id);
        }
    }

    /// <summary>
    ///     JS callback for progress complete or any other relevant event.
    /// </summary>
    [JSInvokable]
    public async Task OnProgressComplete()
    {
        LogDebug("JS invoked OnProgressComplete for {Id}", SnackbarInstance.Id);
        await CloseAsync();
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Called by the base class to do final JS cleanup (if needed).
    ///     For example, disposing or removing the snackbar from the JS manager.
    /// </remarks>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        if (_jsModule is null)
        {
            return;
        }

        try
        {
            // If your JS code has a method to remove the snackbar object,
            // you might call something like:
            await _jsModule.InvokeVoidAsync($"{JsModuleName}API.dispose", SnackbarInstance.Id);

            LogDebug("Snackbar JS object disposed for {Id}", SnackbarInstance.Id);
        }
        catch (JSDisconnectedException)
        {
            LogWarning("Cleanup skipped: JS runtime disconnected.");
        }
        catch (TaskCanceledException)
        {
            LogWarning("Cleanup skipped: Operation cancelled.");
        }
        catch (Exception ex)
        {
            LogWarning("Error disposing JS resources for {Id}: {Message}", ex, SnackbarInstance.Id, ex.Message);
        }
        finally
        {
            _jsModule = null;
        }
    }
}
