#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A Blazor component for displaying snackbar notifications.
/// </summary>
public sealed partial class DropBearSnackbarNotification : DropBearComponentBase, IAsyncDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearSnackbarNotification>();

    private bool _isDismissed;
    private bool _isDisposed;

    // Unique ID for the snackbar instance
    private string SnackbarId { get; } = $"snackbar-{Guid.NewGuid()}";

    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string Message { get; set; } = string.Empty;
    [Parameter] public SnackbarType Type { get; set; } = SnackbarType.Information;
    [Parameter] public int Duration { get; set; } = 5000;
    [Parameter] public bool IsDismissible { get; set; } = true;
    [Parameter] public string ActionText { get; set; } = "Dismiss";
    [Parameter] public EventCallback OnAction { get; set; } = EventCallback.Empty;
    [Parameter] public bool IsVisible { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    /// <summary>
    ///     Handles component disposal, including JS interop cleanup.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        try
        {
            await DisposeSnackbarAsync();
            Logger.Debug("Snackbar disposed successfully for SnackbarId: {SnackbarId}", SnackbarId);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing snackbar for SnackbarId: {SnackbarId}", SnackbarId);
            throw;
        }
    }

    public async Task ShowAsync()
    {
        if (IsVisible)
        {
            Logger.Warning("Attempt to show an already visible snackbar with SnackbarId: {SnackbarId}", SnackbarId);
            return;
        }

        try
        {
            IsVisible = true;
            await InvokeAsync(StateHasChanged);
            await Task.Delay(50); // Small delay to ensure the DOM is updated
            await JsRuntime.InvokeVoidAsync("DropBearSnackbar.startProgress", SnackbarId, Duration);
            Logger.Debug("Snackbar shown successfully for SnackbarId: {SnackbarId}", SnackbarId);
        }
        catch (JSException ex)
        {
            Logger.Warning(ex, "Error showing snackbar with SnackbarId: {SnackbarId}", SnackbarId);
        }
        catch (Exception ex)
        {
            Logger.Error("Error showing snackbar with SnackbarId: {SnackbarId}", SnackbarId);
            throw new SnackbarException("Error showing snackbar", ex);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && IsVisible)
        {
            await ShowAsync();
        }
    }

    /// <summary>
    ///     Dismisses the snackbar.
    /// </summary>
    public async Task DismissAsync()
    {
        if (!IsVisible)
        {
            Logger.Warning("Attempt to dismiss a hidden snackbar with SnackbarId: {SnackbarId}", SnackbarId);
            return;
        }

        try
        {
            await HideSnackbarAsync();
            IsVisible = false;
            _isDismissed = true;
            Logger.Debug("Snackbar dismissed successfully for SnackbarId: {SnackbarId}", SnackbarId);
            StateHasChanged();
        }
        catch (JSException ex)
        {
            Logger.Error(ex, "Error dismissing snackbar with SnackbarId: {SnackbarId}", SnackbarId);
            throw new SnackbarException("Error dismissing snackbar", ex);
        }
    }

    private void Dismiss()
    {
        _ = DismissAsync();
    }

    private async Task OnActionClick()
    {
        await OnAction.InvokeAsync();
        await DismissAsync();
    }

    private string GetSnackbarClasses()
    {
        return $"snackbar-{Type.ToString().ToLowerInvariant()}";
    }

    private string GetIconClass()
    {
        return Type switch
        {
            SnackbarType.Information => "fas fa-info-circle",
            SnackbarType.Success => "fas fa-check-circle",
            SnackbarType.Warning => "fas fa-exclamation-triangle",
            SnackbarType.Error => "fas fa-times-circle",
            _ => "fas fa-info-circle"
        };
    }

    private async Task DisposeSnackbarAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("DropBearSnackbar.disposeSnackbar", SnackbarId);
            Logger.Debug("Snackbar disposed via JS for SnackbarId: {SnackbarId}", SnackbarId);
        }
        catch (JSException ex)
        {
            Logger.Error(ex, "Error during JS disposal of snackbar with SnackbarId: {SnackbarId}", SnackbarId);
            throw new SnackbarException("Error disposing snackbar", ex);
        }
    }

    private async Task HideSnackbarAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("DropBearSnackbar.hideSnackbar", SnackbarId);
            Logger.Debug("Snackbar hidden via JS for SnackbarId: {SnackbarId}", SnackbarId);
        }
        catch (JSException ex)
        {
            Logger.Error(ex, "Error hiding snackbar with SnackbarId: {SnackbarId}", SnackbarId);
            throw new SnackbarException("Error hiding snackbar", ex);
        }
    }
}
