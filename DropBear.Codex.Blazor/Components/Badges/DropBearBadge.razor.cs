using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DropBear.Codex.Blazor.Components.Badges;

/// <summary>
/// A modern, accessible badge component optimized for Blazor Server applications.
/// </summary>
public partial class DropBearBadge : DropBearComponentBase
{
    private readonly string _elementId = $"badge-{Guid.NewGuid():N}";
    private bool showTooltip;
    private string tooltipStyle = string.Empty;
    private IJSObjectReference? _jsModule;

    #region Parameters

    /// <summary>
    /// The color variant of the badge.
    /// </summary>
    [Parameter] public BadgeColor Color { get; set; } = BadgeColor.Default;

    /// <summary>
    /// The shape variant of the badge.
    /// </summary>
    [Parameter] public BadgeShape Shape { get; set; } = BadgeShape.Normal;

    /// <summary>
    /// The size of the badge.
    /// </summary>
    [Parameter] public BadgeSize Size { get; set; } = BadgeSize.Medium;

    /// <summary>
    /// The variant style of the badge.
    /// </summary>
    [Parameter] public BadgeVariant Variant { get; set; } = BadgeVariant.Solid;

    /// <summary>
    /// Icon class name (e.g., Font Awesome classes).
    /// </summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>
    /// Text content to display in the badge.
    /// </summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>
    /// Tooltip text displayed when hovering over the badge.
    /// </summary>
    [Parameter] public string? Tooltip { get; set; }

    /// <summary>
    /// Whether to use native browser tooltips instead of custom ones.
    /// Native tooltips are more accessible and performant.
    /// </summary>
    [Parameter] public bool UseNativeTooltip { get; set; } = true;

    /// <summary>
    /// Additional CSS classes to apply to the badge.
    /// </summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>
    /// Optional ID for the badge element.
    /// </summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>
    /// Child content to render inside the badge (alternative to Text).
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Accessibility label for screen readers.
    /// </summary>
    [Parameter] public string? AccessibilityLabel { get; set; }

    /// <summary>
    /// Whether the badge should be dismissible.
    /// </summary>
    [Parameter] public bool Dismissible { get; set; }

    /// <summary>
    /// Callback fired when the badge is dismissed.
    /// </summary>
    [Parameter] public EventCallback OnDismiss { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the element ID to use for the badge.
    /// </summary>
    protected string ElementId => Id ?? _elementId;

    /// <summary>
    /// Indicates whether the badge has a tooltip.
    /// </summary>
    protected bool HasTooltip => !string.IsNullOrWhiteSpace(Tooltip);

    /// <summary>
    /// Gets the computed CSS classes for the badge.
    /// </summary>
    protected string CssClasses => string.Join(" ", GetCssClasses().Where(c => !string.IsNullOrWhiteSpace(c)));

    /// <summary>
    /// Gets the appropriate ARIA label for accessibility.
    /// </summary>
    protected string? AriaLabel => !string.IsNullOrWhiteSpace(AccessibilityLabel)
        ? AccessibilityLabel
        : !string.IsNullOrWhiteSpace(Text)
            ? $"Badge: {Text}"
            : "Badge";

    #endregion

    #region Lifecycle Methods

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender && HasTooltip && !UseNativeTooltip)
        {
            await InitializeCustomTooltipAsync();
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Generates the CSS classes for the badge.
    /// </summary>
    private IEnumerable<string> GetCssClasses()
    {
        yield return "badge";
        yield return $"badge--{Color.ToString().ToLowerInvariant()}";
        yield return $"badge--{Shape.ToString().ToLowerInvariant()}";
        yield return $"badge--{Size.ToString().ToLowerInvariant()}";
        yield return $"badge--{Variant.ToString().ToLowerInvariant()}";

        if (IsIconOnly)
            yield return "badge--icon-only";

        if (Dismissible)
            yield return "badge--dismissible";

        if (!string.IsNullOrWhiteSpace(Class))
            yield return Class;
    }

    /// <summary>
    /// Determines if this is an icon-only badge.
    /// </summary>
    private bool IsIconOnly => !string.IsNullOrWhiteSpace(Icon) &&
                               string.IsNullOrWhiteSpace(Text) &&
                               ChildContent is null;

    /// <summary>
    /// Initializes custom tooltip functionality.
    /// </summary>
    private async Task InitializeCustomTooltipAsync()
    {
        try
        {
            var jsModuleResult = await GetJsModuleAsync(JsModuleNames.Utils);
            if (jsModuleResult.IsSuccess)
            {
                _jsModule = jsModuleResult.Value;
            }
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize custom tooltip", ex);
            // Fallback to native tooltip
            UseNativeTooltip = true;
        }
    }

    /// <summary>
    /// Shows the custom tooltip on focus (for keyboard accessibility).
    /// </summary>
    private async Task OnTooltipFocus(FocusEventArgs args)
    {
        if (!HasTooltip || UseNativeTooltip || IsDisposed)
            return;

        try
        {
            // For focus events, we'll position the tooltip at a default location
            // since we don't have mouse coordinates
            if (_jsModule is not null)
            {
                var dimensions = await _jsModule.InvokeAsync<dynamic>("DropBearUtilsAPI.getWindowDimensions");
                CalculateTooltipPositionForFocus(dimensions);
            }

            showTooltip = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            LogError("Error showing custom tooltip on focus", ex);
        }
    }

    /// <summary>
    /// Shows the custom tooltip.
    /// </summary>
    private async Task OnTooltipShow(MouseEventArgs args)
    {
        if (!HasTooltip || UseNativeTooltip || IsDisposed)
            return;

        try
        {
            if (_jsModule is not null)
            {
                var dimensions = await _jsModule.InvokeAsync<dynamic>("DropBearUtilsAPI.getWindowDimensions");
                CalculateTooltipPosition(args, dimensions);
            }

            showTooltip = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            LogError("Error showing custom tooltip", ex);
        }
    }

    /// <summary>
    /// Hides the custom tooltip.
    /// </summary>
    private async Task OnTooltipHide()
    {
        if (!showTooltip || IsDisposed)
            return;

        await QueueStateHasChangedAsync(() =>
        {
            showTooltip = false;
            tooltipStyle = string.Empty;
        });
    }

    /// <summary>
    /// Calculates optimal tooltip position.
    /// </summary>
    private void CalculateTooltipPosition(MouseEventArgs args, dynamic dimensions)
    {
        const int tooltipOffset = 8;
        const int tooltipMaxWidth = 200;

        var mouseX = Math.Max(0, Math.Min(args.ClientX, (double)dimensions.width));
        var mouseY = Math.Max(0, Math.Min(args.ClientY, (double)dimensions.height));

        // Simple positioning logic
        var left = mouseX + tooltipMaxWidth > dimensions.width
            ? mouseX - tooltipMaxWidth - tooltipOffset
            : mouseX + tooltipOffset;

        var top = mouseY - 40; // Position above the cursor

        tooltipStyle = $"left: {left}px; top: {top}px;";
    }

    /// <summary>
    /// Calculates tooltip position for focus events (keyboard navigation).
    /// </summary>
    private void CalculateTooltipPositionForFocus(dynamic dimensions)
    {
        // Position tooltip at a reasonable default location for keyboard focus
        // You might want to enhance this to get the actual element position
        var left = Math.Min(100, (double)dimensions.width - 220); // 20px margin from right edge
        var top = 100; // Default top position

        tooltipStyle = $"left: {left}px; top: {top}px;";
    }

    /// <summary>
    /// Handles badge dismissal.
    /// </summary>
    private async Task HandleDismiss()
    {
        if (Dismissible && OnDismiss.HasDelegate)
        {
            await OnDismiss.InvokeAsync();
        }
    }

    #endregion

    #region IAsyncDisposable

    protected override async ValueTask DisposeAsyncCore()
    {
        showTooltip = false;
        await base.DisposeAsyncCore();
    }

    #endregion
}
