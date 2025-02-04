#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Badges;

/// <summary>
///     A Blazor component for displaying badges with optional tooltips.
/// </summary>
public sealed partial class DropBearBadge : DropBearComponentBase
{
    // For positioning the tooltip near the mouse
    private const int TOOLTIP_MARGIN = 10;
    private const int TOOLTIP_MAX_WIDTH = 200;

    // Cached module reference for "dropbear-utils" or whichever JS file
    private IJSObjectReference? _jsModule;
    private const string JsModuleName = JsModuleNames.Utils;

    // Private fields for tooltip visibility and style
    private bool _showTooltip;
    private string _tooltipStyle = string.Empty;

    #region Lifecycle

    /// <inheritdoc />
    /// <remarks>
    ///     On first render, load the module for "dropbear-utils" so we can call getWindowDimensions
    ///     whenever the user hovers over the badge.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
        {
            return;
        }

        try
        {
            // 1) Retrieve the JS module reference once (similar to FileUploader approach).
            //    "dropbear-utils" must match the name in your "import" statement in the .js file path.
            _jsModule = await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);

            LogDebug("DropBearBadge JS module loaded successfully for tooltips.");
        }
        catch (Exception ex)
        {
            LogError("Failed to load dropbear-utils module for DropBearBadge.", ex);
        }
    }

    #endregion

    #region Helpers

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

    #endregion

    #region Disposal

    /// <inheritdoc />
    /// <remarks>
    ///     We override CleanupJavaScriptResourcesAsync if we need to do any final JS disposal,
    ///     but for a simple "getWindowDimensions" call, there's nothing special to cleanup.
    ///     We'll just reset the tooltip state.
    /// </remarks>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        // Hide tooltip
        ShowTooltip = false;
        TooltipStyle = string.Empty;

        // If you had something to do with the JS module (like unsubscribing from an event),
        // you could do it here. For now, just log.
        LogDebug("DropBearBadge cleaning up JS resources (if any).");

        // Let the base handle final disposal (which calls DisposeJsModulesAsync).
        await base.CleanupJavaScriptResourcesAsync();
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Indicates whether the tooltip is currently visible.
    ///     Changing this value triggers a re-render.
    /// </summary>
    protected bool ShowTooltip
    {
        get => _showTooltip;
        set
        {
            if (_showTooltip == value)
            {
                return;
            }

            _showTooltip = value;
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Inline style to position the tooltip (e.g. "left:123px; top:45px;")
    /// </summary>
    protected string TooltipStyle
    {
        get => _tooltipStyle;
        set
        {
            if (_tooltipStyle == value)
            {
                return;
            }

            _tooltipStyle = value;
            StateHasChanged();
        }
    }

    /// <summary>
    ///     Constructs a dynamic CSS class based on badge color, shape, etc.
    /// </summary>
    protected string CssClass => BuildCssClass();

    #endregion

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

    #region Tooltip Handlers

    /// <summary>
    ///     Shows the tooltip near the mouse cursor if a Tooltip is defined.
    /// </summary>
    protected async Task OnTooltipShow(MouseEventArgs args)
    {
        if (string.IsNullOrEmpty(Tooltip) || IsDisposed)
        {
            return;
        }

        try
        {
            // If we haven't loaded or lost the reference, reacquire it
            _jsModule ??= await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);

            // 2) Call "DropBearUtilities.getWindowDimensions" inside that module
            var dimensions = await _jsModule.InvokeAsync<WindowDimensions>(
                $"{JsModuleName}API.getWindowDimensions"
            ).ConfigureAwait(false);

            CalculateTooltipPosition(args, dimensions);
            ShowTooltip = true;

            LogDebug("Tooltip shown at position: X={ClientX}, Y={ClientY}", args.ClientX, args.ClientY);
        }
        catch (Exception ex)
        {
            LogError("Error showing tooltip for badge", ex);
            ShowTooltip = false;
            TooltipStyle = string.Empty;
        }
    }

    /// <summary>
    ///     Calculates the optimal position for the tooltip based on window dimensions.
    /// </summary>
    private void CalculateTooltipPosition(MouseEventArgs args, WindowDimensions windowDimensions)
    {
        // Avoid viewport overflow on the X-axis
        var offsetX = args.ClientX + TOOLTIP_MAX_WIDTH > windowDimensions.Width
            ? -TOOLTIP_MAX_WIDTH
            : TOOLTIP_MARGIN;

        // Avoid overflow on Y-axis. 50 is an estimate for tooltip height
        var offsetY = args.ClientY + 50 > windowDimensions.Height
            ? -50
            : TOOLTIP_MARGIN;

        TooltipStyle = $"left: {args.ClientX + offsetX}px; top: {args.ClientY + offsetY}px;";
    }

    /// <summary>
    ///     Hides the tooltip when the mouse pointer leaves the badge.
    /// </summary>
    protected void OnTooltipHide()
    {
        ShowTooltip = false;
        TooltipStyle = string.Empty;
        LogDebug("Tooltip hidden.");
    }

    #endregion
}
