#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A Blazor component for displaying page alerts.
/// </summary>
public sealed partial class DropBearPageAlert : DropBearComponentBase
{
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
        if (!IsDismissible || !OnClose.HasDelegate)
        {
            return;
        }

        await OnClose.InvokeAsync();
    }
}
