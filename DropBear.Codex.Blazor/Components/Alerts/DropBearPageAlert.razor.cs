﻿#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearPageAlert : DropBearComponentBase
{
    private static readonly Dictionary<PageAlertType, string> IconPaths = new()
    {
        { PageAlertType.Success, "<path d=\"M20 6L9 17L4 12\"></path>" },
        {
            PageAlertType.Error,
            "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle><path d=\"M15 9l-6 6M9 9l6 6\"></path>"
        },
        { PageAlertType.Warning, "<path d=\"M12 9v2m0 4h.01\"></path><path d=\"M12 5l7 13H5l7-13z\"></path>" },
        {
            PageAlertType.Info,
            "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle><path d=\"M12 16v-4m0-4h.01\"></path>"
        }
    };

    private string Id => AlertId!;

    [Parameter] [EditorRequired] public string? AlertId { get; set; } = null!;

    [Parameter] [EditorRequired] public string? Title { get; set; } = null!;

    [Parameter] [EditorRequired] public string? Message { get; set; } = null!;

    [Parameter] public PageAlertType Type { get; set; } = PageAlertType.Info;

    [Parameter] public bool IsPermanent { get; set; }

    [Parameter] public int? Duration { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }

    private string AlertTypeCssClass => Type.ToString().ToLower();

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
            Logger.Debug("Initializing alert with ID: {AlertId}", Id);
            var createResult = await SafeJsInteropAsync<bool>("DropBearPageAlert.create", Id, Duration, IsPermanent);
            if (!createResult)
            {
                Logger.Warning("Failed to create alert with ID: {AlertId}", AlertId);
            }
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
        return IconPaths.TryGetValue(Type, out var path) ? path : string.Empty;
    }


    public override async ValueTask DisposeAsync()
    {
        try
        {
            if (!IsDisposed)
            {
                var result = await SafeJsInteropAsync<bool>("DropBearPageAlert.hide", Id);
                if (!result)
                {
                    Logger.Warning("Failed to hide alert with ID: {AlertId}", AlertId);
                }
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

    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(AlertId))
        {
            throw new ArgumentException("AlertId cannot be null or empty.", nameof(AlertId));
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Title cannot be null or empty.", nameof(Title));
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(Message));
        }
    }
}
