#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearPageAlert : DropBearComponentBase
{
    private static readonly IReadOnlyDictionary<AlertType, (string Icon, string AriaLabel)> AlertConfigs =
        new Dictionary<AlertType, (string Icon, string AriaLabel)>
        {
            { AlertType.Information, ("fas fa-info-circle", "Information alert") },
            { AlertType.Success, ("fas fa-check-circle", "Success alert") },
            { AlertType.Warning, ("fas fa-exclamation-triangle", "Warning alert") },
            { AlertType.Danger, ("fas fa-times-circle", "Error alert") },
            { AlertType.Notification, ("fas fa-bell", "Notification alert") }
        };

    private readonly string _alertId;

    private string? _alertClass;
    private string? _iconClass;
    private bool _isClosing;

    public DropBearPageAlert()
    {
        _alertId = $"alert-{ComponentId}";
    }

    [Parameter] [EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter] [EditorRequired] public string Message { get; set; } = string.Empty;
    [Parameter] public AlertType Type { get; set; } = AlertType.Information;
    [Parameter] public bool IsDismissible { get; set; } = true;
    [Parameter] public EventCallback OnClose { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    protected override void OnParametersSet()
    {
        if (IsDisposed)
        {
            return;
        }

        var alertType = Type.ToString().ToLowerInvariant();
        _alertClass = $"alert alert-{alertType}";
        _iconClass = AlertConfigs[Type].Icon;
    }

    private async Task HandleCloseClick()
    {
        if (IsDisposed || _isClosing || !IsDismissible)
        {
            Logger.Warning(
                "Invalid close attempt - Alert: {Title}, Type: {Type}, Dismissible: {IsDismissible}, Closing: {IsClosing}, Disposed: {IsDisposed}",
                Title, Type, IsDismissible, _isClosing, IsDisposed);
            return;
        }

        await InvokeStateHasChangedAsync(async () =>
        {
            try
            {
                _isClosing = true;

                if (OnClose.HasDelegate)
                {
                    try
                    {
                        await SafeJsVoidInteropAsync("alert.startExitAnimation", Type);
                        await Task.Delay(300); // Match animation duration
                    }
                    catch (JSDisconnectedException)
                    {
                        // Circuit disconnected, proceed with close
                        Logger.Debug("Circuit disconnected during alert animation, proceeding with close");
                    }

                    if (!IsDisposed)
                    {
                        await OnClose.InvokeAsync();
                        Logger.Debug("Alert closed successfully - Type: {Type}, Title: {Title}", Type, Title);
                    }
                }
                else
                {
                    Logger.Warning("No close handler attached - Type: {Type}, Title: {Title}", Type, Title);
                }
            }
            finally
            {
                _isClosing = false;
            }
        });
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (!_isClosing && IsDismissible && OnClose.HasDelegate)
            {
                await SafeJsVoidInteropAsync("alert.cleanup", _alertId);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error during alert cleanup: {AlertId}", _alertId);
        }
    }

    private string GetAriaLabel()
    {
        return AlertConfigs[Type].AriaLabel;
    }
}
