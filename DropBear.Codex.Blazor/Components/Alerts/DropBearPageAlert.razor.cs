#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A Blazor component for displaying page alerts.
/// </summary>
public sealed partial class DropBearPageAlert : DropBearComponentBase, IDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearPageAlert>();

    private static readonly Dictionary<AlertType, string> IconClasses = new()
    {
        { AlertType.Information, "fas fa-info-circle" },
        { AlertType.Success, "fas fa-check-circle" },
        { AlertType.Warning, "fas fa-exclamation-triangle" },
        { AlertType.Danger, "fas fa-times-circle" },
        { AlertType.Notification, "fas fa-bell" }
    };

    private string _alertClassString = string.Empty;
    private string _iconClassString = string.Empty;
    private bool _shouldRender = true;

    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string Message { get; set; } = string.Empty;
    [Parameter] public AlertType Type { get; set; } = AlertType.Information;
    [Parameter] public bool IsDismissible { get; set; } = true;
    [Parameter] public EventCallback OnClose { get; set; } = EventCallback.Empty;

    public void Dispose()
    {
        // Dispose logic here if needed in the future
    }

    protected override void OnParametersSet()
    {
        _alertClassString = $"alert alert-{Type.ToString().ToLowerInvariant()}";
        _iconClassString = IconClasses[Type];
    }

    protected override bool ShouldRender()
    {
        return _shouldRender;
    }

    private void SetStateChanged()
    {
        _shouldRender = true;
        StateHasChanged();
    }

    /// <summary>
    ///     Handles the close button click event.
    /// </summary>
    private async Task OnCloseClick()
    {
        if (!IsDismissible)
        {
            Logger.Warning(
                "Non-dismissible alert of type {AlertType} with title '{AlertTitle}' attempted to be closed.", Type,
                Title);
            return;
        }

        if (!OnClose.HasDelegate)
        {
            Logger.Warning("Alert of type {AlertType} with title '{AlertTitle}' has no close delegate attached.", Type,
                Title);
            return;
        }

        try
        {
            await OnClose.InvokeAsync();
            Logger.Debug("Alert of type {AlertType} with title '{AlertTitle}' closed successfully.", Type, Title);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while closing the alert of type {AlertType} with title '{AlertTitle}'.",
                Type, Title);
        }
    }
}
