#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Exceptions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A Blazor component for displaying snackbar notifications.
/// </summary>
public sealed partial class DropBearSnackbarNotification : DropBearComponentBase, IAsyncDisposable
{
    private bool _isDismissed;
    private bool _isDisposed;
    private bool _shouldRender;


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
        if (!_isDisposed)
        {
            _isDisposed = true;
            await DisposeSnackbarAsync();
        }
    }

    /// <summary>
    ///     Dismisses the snackbar.
    /// </summary>
    public async Task DismissAsync()
    {
        if (!IsVisible)
        {
            return;
        }

        try
        {
            await HideSnackbarAsync();
        }
        catch (JSException ex)
        {
            throw new SnackbarException("Error dismissing snackbar", ex);
        }

        IsVisible = false;
        _isDismissed = true;
        _shouldRender = true;
        StateHasChanged();
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
        }
        catch (JSException ex)
        {
            throw new SnackbarException("Error disposing snackbar", ex);
        }
    }

    private async Task HideSnackbarAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("DropBearSnackbar.hideSnackbar", SnackbarId);
        }
        catch (JSException ex)
        {
            throw new SnackbarException("Error hiding snackbar", ex);
        }
    }
}
