#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearPageAlert : DropBearComponentBase
{
    private DotNetObjectReference<DropBearPageAlert>? _reference;
    private string Id => $"pagealert-{AlertId}";

    [Parameter] [EditorRequired] public string AlertId { get; set; } = null!;

    [Parameter] [EditorRequired] public string Title { get; set; } = null!;

    [Parameter] [EditorRequired] public string Message { get; set; } = null!;

    [Parameter] public PageAlertType Type { get; set; } = PageAlertType.Info;

    [Parameter] public bool IsPermanent { get; set; }

    [Parameter] public int? Duration { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }

    protected override void OnInitialized()
    {
        _reference = DotNetObjectReference.Create(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeAlert();
        }
    }

    private async Task InitializeAlert()
    {
        try
        {
            await SafeJsInteropAsync<bool>("DropBearPageAlert.create", Id, Duration, IsPermanent);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize page alert {AlertId}", AlertId);
        }
    }

    private async Task RequestClose()
    {
        try
        {
            await OnClose.InvokeAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error closing page alert {AlertId}", AlertId);
        }
    }

    private string GetIconPath()
    {
        return Type switch
        {
            PageAlertType.Success => "<path d=\"M20 6L9 17L4 12\"></path>",
            PageAlertType.Error =>
                "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle><path d=\"M15 9l-6 6M9 9l6 6\"></path>",
            PageAlertType.Warning => "<path d=\"M12 9v2m0 4h.01\"></path><path d=\"M12 5l7 13H5l7-13z\"></path>",
            PageAlertType.Info => "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle><path d=\"M12 16v-4m0-4h.01\"></path>",
            _ => "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle><path d=\"M12 16v-4m0-4h.01\"></path>"
        };
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            if (!IsDisposed)
            {
                await SafeJsInteropAsync<bool>("DropBearPageAlert.hide", Id);
                _reference?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing page alert {AlertId}", AlertId);
        }
        finally
        {
            await base.DisposeAsync();
        }
    }
}
