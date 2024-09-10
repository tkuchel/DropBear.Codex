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
public sealed partial class DropBearPageAlert : DropBearComponentBase
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

    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string Message { get; set; } = string.Empty;
    [Parameter] public AlertType Type { get; set; } = AlertType.Information;
    [Parameter] public bool IsDismissible { get; set; } = true;
    [Parameter] public EventCallback OnClose { get; set; } = EventCallback.Empty;

    private string AlertClassString => $"alert alert-{Type.ToString().ToLowerInvariant()}";
    private string IconClassString => IconClasses[Type];

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
            Logger.Information("Alert of type {AlertType} with title '{AlertTitle}' closed successfully.", Type, Title);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while closing the alert of type {AlertType} with title '{AlertTitle}'.",
                Type, Title);
        }
    }
}
