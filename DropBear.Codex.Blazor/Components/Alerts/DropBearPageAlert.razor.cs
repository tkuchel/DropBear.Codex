#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

public sealed partial class DropBearPageAlert : DropBearComponentBase, IDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger
        .ForContext<DropBearPageAlert>();

    private static readonly IReadOnlyDictionary<AlertType, (string Icon, string AriaLabel)> AlertConfigs =
        new Dictionary<AlertType, (string Icon, string AriaLabel)>
        {
            { AlertType.Information, ("fas fa-info-circle", "Information alert") },
            { AlertType.Success, ("fas fa-check-circle", "Success alert") },
            { AlertType.Warning, ("fas fa-exclamation-triangle", "Warning alert") },
            { AlertType.Danger, ("fas fa-times-circle", "Error alert") },
            { AlertType.Notification, ("fas fa-bell", "Notification alert") }
        };

    private string? _alertClass;
    private string? _iconClass;
    private bool _isClosing;
    private bool _isDisposed;

    [Parameter] [EditorRequired] public string Title { get; set; } = string.Empty;

    [Parameter] [EditorRequired] public string Message { get; set; } = string.Empty;

    [Parameter] public AlertType Type { get; set; } = AlertType.Information;

    [Parameter] public bool IsDismissible { get; set; } = true;

    [Parameter] public EventCallback OnClose { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Logger.Debug("Alert disposed - Type: {Type}, Title: {Title}", Type, Title);
    }

    protected override void OnParametersSet()
    {
        if (_isDisposed)
        {
            return;
        }

        var alertType = Type.ToString().ToLowerInvariant();
        _alertClass = $"alert alert-{alertType}";
        _iconClass = AlertConfigs[Type].Icon;
    }

    private async Task HandleCloseClick()
    {
        if (_isDisposed || _isClosing || !IsDismissible)
        {
            Logger.Warning(
                "Invalid close attempt - Alert: {Title}, Type: {Type}, Dismissible: {IsDismissible}, Closing: {IsClosing}, Disposed: {IsDisposed}",
                Title, Type, IsDismissible, _isClosing, _isDisposed);
            return;
        }

        try
        {
            _isClosing = true;

            if (OnClose.HasDelegate)
            {
                try
                {
                    await JsRuntime.InvokeVoidAsync("alert.startExitAnimation", Type);
                }
                catch (JSDisconnectedException)
                {
                    // Circuit disconnected, proceed with close
                }

                if (!_isDisposed)
                {
                    await Task.Delay(300); // Match animation duration
                    await OnClose.InvokeAsync();
                    Logger.Debug("Alert closed successfully - Type: {Type}, Title: {Title}", Type, Title);
                }
            }
            else
            {
                Logger.Warning("No close handler attached - Type: {Type}, Title: {Title}", Type, Title);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error closing alert - Type: {Type}, Title: {Title}", Type, Title);
            throw;
        }
        finally
        {
            _isClosing = false;
        }
    }

    private string GetAriaLabel()
    {
        return AlertConfigs[Type].AriaLabel;
    }
}
