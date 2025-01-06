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
    // Static logger reference (Serilog).
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearBadge>();


    /// <summary>
    ///     Specifies the badge color (e.g., Default, Primary, Success, etc.).
    /// </summary>
    [Parameter]
    public BadgeColor Color { get; set; } = BadgeColor.Default;

    /// <summary>
    ///     Specifies the shape of the badge (e.g., Normal, Pill).
    /// </summary>
    [Parameter]
    public BadgeShape Shape { get; set; } = BadgeShape.Normal;

    /// <summary>
    ///     Optional icon class name (e.g., Font Awesome) to display in the badge.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    ///     Text to display inside the badge.
    /// </summary>
    [Parameter]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    ///     Tooltip text displayed when hovering over the badge.
    /// </summary>
    [Parameter]
    public string Tooltip { get; set; } = string.Empty;

    /// <summary>
    ///     Indicates whether the tooltip is currently visible.
    /// </summary>
    private bool ShowTooltip { get; set; }

    /// <summary>
    ///     Inline style to position the tooltip (e.g., "left: XXpx; top: YYpx;").
    /// </summary>
    private string TooltipStyle { get; set; } = string.Empty;

    /// <summary>
    ///     Constructs a dynamic CSS class based on badge color, shape, etc.
    /// </summary>
    private string CssClass => BuildCssClass();

    /// <summary>
    ///     Called when this component is disposed, ensuring cleanup of tooltip state or JS interop.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            ShowTooltip = false;
            TooltipStyle = string.Empty;
            Logger.Debug("Disposing DropBearBadge component.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during disposal of DropBearBadge component.");
        }

        // If you need to do any additional JS cleanup,
        // you could do so here (e.g., calling a JS function to remove event listeners).
        await ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Builds the base CSS class string for the badge.
    /// </summary>
    private string BuildCssClass()
    {
        var cssClass = "dropbear-badge";
        cssClass += $" dropbear-badge-{Color.ToString().ToLowerInvariant()}";
        cssClass += $" dropbear-badge-{Shape.ToString().ToLowerInvariant()}";

        // If there is no text but we do have an icon, apply a special class.
        if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Icon))
        {
            cssClass += " dropbear-badge-icon-only";
        }

        return cssClass.Trim();
    }

    /// <summary>
    ///     Shows the tooltip near the mouse cursor if a Tooltip is defined.
    /// </summary>
    private async Task OnTooltipShow(MouseEventArgs args)
    {
        if (string.IsNullOrEmpty(Tooltip))
        {
            return;
        }

        ShowTooltip = true;

        try
        {
            // Example: Get window dimensions from JS (you'll need the JS function 'getWindowDimensions' implemented).
            var windowDimensions = await JsRuntime.InvokeAsync<WindowDimensions>("getWindowDimensions");

            // Attempt to position tooltip so it won't overflow the viewport.
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
    ///     Hides the tooltip when the mouse pointer leaves the badge.
    /// </summary>
    private void OnTooltipHide()
    {
        ShowTooltip = false;
        TooltipStyle = string.Empty;

        Logger.Debug("Tooltip hidden.");
        StateHasChanged();
    }
}


