#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Badges;

/// <summary>
///     A Blazor component for displaying badges with optional tooltips.
/// </summary>
public sealed partial class DropBearBadge : DropBearComponentBase
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearBadge>();

    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    [Parameter] public BadgeColor Color { get; set; } = BadgeColor.Default;
    [Parameter] public BadgeShape Shape { get; set; } = BadgeShape.Normal;
    [Parameter] public string Icon { get; set; } = string.Empty;
    [Parameter] public string Text { get; set; } = string.Empty;
    [Parameter] public string Tooltip { get; set; } = string.Empty;

    private bool ShowTooltip { get; set; }
    private string TooltipStyle { get; set; } = string.Empty;

    private string CssClass => BuildCssClass();

    /// <summary>
    ///     Builds the CSS class for the badge based on its properties.
    /// </summary>
    /// <returns>A string representing the CSS class.</returns>
    private string BuildCssClass()
    {
        var cssClass = "dropbear-badge";
        cssClass += $" dropbear-badge-{Color.ToString().ToLowerInvariant()}";
        cssClass += $" dropbear-badge-{Shape.ToString().ToLowerInvariant()}";

        if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Icon))
        {
            cssClass += " dropbear-badge-icon-only";
        }

        return cssClass.Trim();
    }

    /// <summary>
    ///     Shows the tooltip at the specified mouse position.
    /// </summary>
    /// <param name="args">The mouse event arguments.</param>
    private async Task OnTooltipShow(MouseEventArgs args)
    {
        if (string.IsNullOrEmpty(Tooltip))
        {
            return;
        }

        ShowTooltip = true;

        try
        {
            // Get the window dimensions using JavaScript interop
            var windowDimensions = await JsRuntime.InvokeAsync<WindowDimensions>("getWindowDimensions");

            var offsetX = args.ClientX + 200 > windowDimensions.Width ? -200 : 10;
            var offsetY = args.ClientY + 50 > windowDimensions.Height ? -50 : 10;

            TooltipStyle = $"left: {args.ClientX + offsetX}px; top: {args.ClientY + offsetY}px;";
            Logger.Debug("Tooltip shown at position: X={ClientX}, Y={ClientY}", args.ClientX, args.ClientY);
        }
        catch (JSException ex)
        {
            Logger.Error(ex, "Error showing tooltip for badge.");
        }
        finally
        {
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Hides the tooltip.
    /// </summary>
    private void OnTooltipHide()
    {
        ShowTooltip = false;
        Logger.Debug("Tooltip hidden.");
        StateHasChanged();
    }
}
