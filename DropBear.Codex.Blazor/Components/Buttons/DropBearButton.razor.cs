#region

using System.Runtime.CompilerServices;
using System.Text;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

/// <summary>
///     A Blazor component for rendering a button with various style, size, and color options.
///     Optimized for Blazor Server performance and memory usage.
/// </summary>
public sealed partial class DropBearButton : DropBearComponentBase
{
    // Throttling for rapid clicks
    private static readonly TimeSpan ClickThrottleDelay = TimeSpan.FromMilliseconds(50);


    // Track parameter changes to know when to rebuild CSS class
    private ButtonStyle _lastButtonStyle;
    private RenderFragment? _lastChildContent;
    private DateTime _lastClickTime = DateTime.MinValue;
    private ButtonColor _lastColor;
    private string _lastIcon = string.Empty;
    private bool _lastIsBlock;
    private bool _lastIsDisabled;
    private ButtonSize _lastSize;

    /// <summary>
    ///     Constructs a dynamic CSS class based on button parameters.
    /// </summary>
    private string CssClass { get; set; } = string.Empty;

    /// <summary>
    ///     Updates parameters and recalculates CSS class only when needed
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // Only rebuild CSS class if relevant parameters changed
        if (CssClass == string.Empty ||
            _lastButtonStyle != ButtonStyle ||
            _lastColor != Color ||
            _lastSize != Size ||
            _lastIsBlock != IsBlock ||
            _lastIsDisabled != IsDisabled ||
            _lastIcon != Icon ||
            _lastChildContent != ChildContent)
        {
            CssClass = BuildCssClass();

            // Update cached values
            _lastButtonStyle = ButtonStyle;
            _lastColor = Color;
            _lastSize = Size;
            _lastIsBlock = IsBlock;
            _lastIsDisabled = IsDisabled;
            _lastIcon = Icon;
            _lastChildContent = ChildContent;
        }
    }

    /// <summary>
    ///     Builds the CSS class for the button based on its configuration.
    ///     This method is optimized to reduce string allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string BuildCssClass()
    {
        var builder = new StringBuilder(256);

        // Add base class
        builder.Append("dropbear-btn");

        // Add style variant
        builder.Append(" dropbear-btn-");
        builder.Append(ButtonStyle.ToString().ToLowerInvariant());

        // Add color variant
        builder.Append(" dropbear-btn-");
        builder.Append(Color.ToString().ToLowerInvariant());

        // Add size variant
        builder.Append(" dropbear-btn-");
        builder.Append(Size.ToString().ToLowerInvariant());

        // Add block class if needed
        if (IsBlock)
        {
            builder.Append(" dropbear-btn-block");
        }

        // Add disabled class if needed
        if (IsDisabled)
        {
            builder.Append(" dropbear-btn-disabled");
        }

        // Add icon-only class if needed
        if (!string.IsNullOrEmpty(Icon) && ChildContent is null)
        {
            builder.Append(" dropbear-btn-icon-only");
        }

        // Add any custom class from AdditionalAttributes
        if (AdditionalAttributes.TryGetValue("class", out var customClass) && customClass is string cssClass &&
            !string.IsNullOrEmpty(cssClass))
        {
            builder.Append(' ');
            builder.Append(cssClass);

            // Remove from AdditionalAttributes to avoid duplication
            AdditionalAttributes.Remove("class");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Handles the button click event, respecting the disabled state and implementing throttling.
    /// </summary>
    private async Task OnClickHandler(MouseEventArgs args)
    {
        if (IsDisposed)
        {
            Logger.Warning("Click ignored - button component is disposed.");
            return;
        }

        if (IsDisabled)
        {
            Logger.Debug("Click ignored - button is disabled.");
            return;
        }

        // Implement click throttling to prevent unintended double-clicks
        var now = DateTime.UtcNow;
        if (now - _lastClickTime < ClickThrottleDelay)
        {
            Logger.Debug("Click throttled - too many clicks in quick succession.");
            return;
        }

        _lastClickTime = now;

        if (!OnClick.HasDelegate)
        {
            return;
        }

        try
        {
            await QueueStateHasChangedAsync(async () =>
            {
                Logger.Debug("Button clicked: {Color} {ButtonStyle} {Size}", Color, ButtonStyle, Size);
                await OnClick.InvokeAsync(args);
            });
        }
        catch (ObjectDisposedException objDisposed)
        {
            // Handle case where component is disposed during click
            Logger.Warning(objDisposed, "Click ignored - button component was disposed during click handling.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling button click: {Color} {ButtonStyle}", Color, ButtonStyle);
        }
    }

    /// <summary>
    ///     Disposes of the component, cleaning up any resources.
    /// </summary>
    public void Dispose()
    {
        // Clear collections to prevent memory leaks
        AdditionalAttributes.Clear();

        // Clear cached values
        CssClass = string.Empty;
        _lastChildContent = null;
    }

    #region Parameters

    /// <summary>
    ///     Determines the overall style of the button (e.g., Solid, Outline).
    /// </summary>
    [Parameter]
    public ButtonStyle ButtonStyle { get; set; } = ButtonStyle.Solid;

    /// <summary>
    ///     Determines the color variant of the button (e.g., Primary, Secondary, Default).
    /// </summary>
    [Parameter]
    public ButtonColor Color { get; set; } = ButtonColor.Default;

    /// <summary>
    ///     Determines the size of the button (e.g., Small, Medium, Large).
    /// </summary>
    [Parameter]
    public ButtonSize Size { get; set; } = ButtonSize.Medium;

    /// <summary>
    ///     If true, the button stretches to fill the container's width.
    /// </summary>
    [Parameter]
    public bool IsBlock { get; set; }

    /// <summary>
    ///     If true, the button is displayed in a disabled state and cannot be clicked.
    /// </summary>
    [Parameter]
    public bool IsDisabled { get; set; }

    /// <summary>
    ///     An optional icon class (e.g., FontAwesome, Material Icons) to display in the button.
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    ///     The button's 'type' attribute (e.g., "button", "submit", "reset").
    /// </summary>
    [Parameter]
    public string Type { get; set; } = "button";

    /// <summary>
    ///     Callback invoked when the button is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    ///     Child content to display within the button, typically text or other markup.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Additional attributes (e.g., data-*, aria-*) to be splatted onto the underlying button.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    #endregion
}
