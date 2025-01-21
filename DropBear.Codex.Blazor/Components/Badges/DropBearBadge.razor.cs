#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

#endregion

namespace DropBear.Codex.Blazor.Components.Badges;

/// <summary>
///     A Blazor component for displaying badges with optional tooltips.
/// </summary>
public sealed partial class DropBearBadge : DropBearComponentBase
{
    private const int TOOLTIP_MARGIN = 10;
    private const int TOOLTIP_MAX_WIDTH = 200;
    private bool _showTooltip;

    private string _tooltipStyle = string.Empty;

    /// <summary>
    ///     Indicates whether the tooltip is currently visible.
    /// </summary>
    private bool ShowTooltip
    {
        get => _showTooltip;
        set
        {
            if (_showTooltip != value)
            {
                _showTooltip = value;
                StateHasChanged();
            }
        }
    }

    /// <summary>
    ///     Inline style to position the tooltip
    /// </summary>
    private string TooltipStyle
    {
        get => _tooltipStyle;
        set
        {
            if (_tooltipStyle != value)
            {
                _tooltipStyle = value;
                StateHasChanged();
            }
        }
    }

    /// <summary>
    ///     Constructs a dynamic CSS class based on badge color, shape, etc.
    /// </summary>
    private string CssClass => BuildCssClass();

    /// <summary>
    ///     Shows the tooltip near the mouse cursor if a Tooltip is defined.
    /// </summary>
    private async Task OnTooltipShow(MouseEventArgs args)
    {
        if (string.IsNullOrEmpty(Tooltip) || IsDisposed)
        {
            return;
        }

        try
        {
            var dimensions = await SafeJsInteropAsync<WindowDimensions>("getWindowDimensions");
            CalculateTooltipPosition(args, dimensions);
            ShowTooltip = true;

            Logger.Debug("Tooltip shown at position: X={ClientX}, Y={ClientY}", args.ClientX, args.ClientY);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error showing tooltip for badge");
            ShowTooltip = false;
            TooltipStyle = string.Empty;
        }
    }

    /// <summary>
    ///     Calculates the optimal position for the tooltip based on window dimensions
    /// </summary>
    private void CalculateTooltipPosition(MouseEventArgs args, WindowDimensions windowDimensions)
    {
        // Calculate offsets to prevent viewport overflow
        var offsetX = args.ClientX + TOOLTIP_MAX_WIDTH > windowDimensions.Width
            ? -TOOLTIP_MAX_WIDTH
            : TOOLTIP_MARGIN;

        var offsetY = args.ClientY + 50 > windowDimensions.Height
            ? -50
            : TOOLTIP_MARGIN;

        TooltipStyle = $"left: {args.ClientX + offsetX}px; top: {args.ClientY + offsetY}px;";
    }

    /// <summary>
    ///     Hides the tooltip when the mouse pointer leaves the badge.
    /// </summary>
    private void OnTooltipHide()
    {
        ShowTooltip = false;
        TooltipStyle = string.Empty;
        Logger.Debug("Tooltip hidden");
    }

    /// <summary>
    ///     Builds the base CSS class string for the badge.
    /// </summary>
    private string BuildCssClass()
    {
        var cssClass = new List<string>
        {
            "dropbear-badge",
            $"dropbear-badge-{Color.ToString().ToLowerInvariant()}",
            $"dropbear-badge-{Shape.ToString().ToLowerInvariant()}"
        };

        // Add icon-only class if applicable
        if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Icon))
        {
            cssClass.Add("dropbear-badge-icon-only");
        }

        return string.Join(" ", cssClass);
    }

    /// <inheritdoc />
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        ShowTooltip = false;
        TooltipStyle = string.Empty;
        await base.CleanupJavaScriptResourcesAsync();
    }

    #region Parameters

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

    #endregion
}
