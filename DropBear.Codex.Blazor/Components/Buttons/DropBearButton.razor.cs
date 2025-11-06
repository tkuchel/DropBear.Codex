#region

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

/// <summary>
///     A modern, optimized Blazor button component with enhanced accessibility and performance.
///     Leverages .NET 9+ features and optimized for Blazor Server applications.
/// </summary>
public sealed partial class DropBearButton : DropBearComponentBase
{
    // Use frozen collections for better performance in .NET 9+
    private static readonly Dictionary<ButtonStyle, string> StyleClasses = new()
    {
        [ButtonStyle.Solid] = "dropbear-btn--solid",
        [ButtonStyle.Outline] = "dropbear-btn--outline",
        [ButtonStyle.Ghost] = "dropbear-btn--ghost",
        [ButtonStyle.Link] = "dropbear-btn--link"
    };

    private static readonly Dictionary<ButtonColor, string> ColorClasses = new()
    {
        [ButtonColor.Default] = "dropbear-btn--default",
        [ButtonColor.Primary] = "dropbear-btn--primary",
        [ButtonColor.Secondary] = "dropbear-btn--secondary",
        [ButtonColor.Success] = "dropbear-btn--success",
        [ButtonColor.Warning] = "dropbear-btn--warning",
        [ButtonColor.Error] = "dropbear-btn--error",
        [ButtonColor.Information] = "dropbear-btn--information"
    };

    private static readonly Dictionary<ButtonSize, string> SizeClasses = new()
    {
        [ButtonSize.XSmall] = "dropbear-btn--xs",
        [ButtonSize.Small] = "dropbear-btn--sm",
        [ButtonSize.Medium] = "dropbear-btn--md",
        [ButtonSize.Large] = "dropbear-btn--lg",
        [ButtonSize.XLarge] = "dropbear-btn--xl"
    };

    // Improved throttling with configurable delay
    private static readonly TimeSpan DefaultClickThrottleDelay = TimeSpan.FromMilliseconds(150);

    // Cached values for change detection
    private string _computedCssClass = string.Empty;
    private int _lastParametersHash;
    private DateTime _lastClickTime = DateTime.MinValue;
    private bool _isProcessingClick;

    /// <summary>
    /// Gets the computed CSS class string, rebuilt only when parameters change
    /// </summary>
    private string CssClass
    {
        get
        {
            var currentHash = GetParametersHash();
            if (_computedCssClass.Length == 0 || _lastParametersHash != currentHash)
            {
                _computedCssClass = BuildCssClass();
                _lastParametersHash = currentHash;
            }
            return _computedCssClass;
        }
    }

    /// <summary>
    /// Computes a hash of all style-affecting parameters for change detection
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetParametersHash()
    {
        var hash = new HashCode();
        hash.Add(ButtonStyle);
        hash.Add(Color);
        hash.Add(Size);
        hash.Add(IsBlock);
        hash.Add(IsDisabled);
        hash.Add(IsLoading);
        hash.Add(Icon);
        hash.Add(ChildContent?.GetHashCode() ?? 0);

        // Include custom class if present
        if (AdditionalAttributes.TryGetValue("class", out var customClass))
        {
            hash.Add(customClass?.GetHashCode() ?? 0);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Builds the CSS class string using modern StringBuilder with capacity estimation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string BuildCssClass()
    {
        // Pre-calculate capacity to avoid StringBuilder reallocations
        const int baseCapacity = 200;
        var builder = new StringBuilder(baseCapacity);

        // Base class
        builder.Append("dropbear-btn");

        // Style variant
        if (StyleClasses.TryGetValue(ButtonStyle, out var styleClass))
        {
            builder.Append(' ').Append(styleClass);
        }

        // Color variant
        if (ColorClasses.TryGetValue(Color, out var colorClass))
        {
            builder.Append(' ').Append(colorClass);
        }

        // Size variant
        if (SizeClasses.TryGetValue(Size, out var sizeClass))
        {
            builder.Append(' ').Append(sizeClass);
        }

        // State classes
        if (IsBlock)
            builder.Append(" dropbear-btn--block");

        if (IsDisabled)
            builder.Append(" dropbear-btn--disabled");

        if (IsLoading)
            builder.Append(" dropbear-btn--loading");

        // Icon-only class
        if (!string.IsNullOrEmpty(Icon) && ChildContent is null)
            builder.Append(" dropbear-btn--icon-only");

        // Custom class from attributes
        if (AdditionalAttributes.TryGetValue("class", out var customClass) &&
            customClass is string cssClass && !string.IsNullOrEmpty(cssClass))
        {
            builder.Append(' ').Append(cssClass);
            // Remove to prevent duplication in DOM
            AdditionalAttributes.Remove("class");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Enhanced click handler with improved throttling and loading state support
    /// </summary>
    private async Task OnClickHandler(MouseEventArgs args)
    {
        if (IsDisposed || IsDisabled || IsLoading || _isProcessingClick)
        {
            return;
        }

        // Enhanced throttling
        var now = DateTime.UtcNow;
        var throttleDelay = ClickThrottleDelay ?? DefaultClickThrottleDelay;
        if (now - _lastClickTime < throttleDelay)
        {
            return;
        }

        _lastClickTime = now;

        if (!OnClick.HasDelegate)
        {
            return;
        }

        try
        {
            _isProcessingClick = true;

            // Show loading state if configured
            if (ShowLoadingOnClick)
            {
                await QueueStateHasChangedAsync(() =>
                {
                    IsLoading = true;
                });
            }

            await OnClick.InvokeAsync(args);
        }
        catch (Exception ex) when (!IsDisposed)
        {
            LogError(ex, "Error handling button click");

            // Notify error callback if provided
            if (OnError.HasDelegate)
            {
                await OnError.InvokeAsync(ex);
            }
        }
        finally
        {
            _isProcessingClick = false;

            if (ShowLoadingOnClick && !IsDisposed)
            {
                await QueueStateHasChangedAsync(() =>
                {
                    IsLoading = false;
                });
            }
        }
    }

    /// <summary>
    /// Gets the appropriate button type attribute value
    /// </summary>
    private string ButtonType => IsDisabled ? "button" : Type;

    /// <summary>
    /// Gets the appropriate aria-disabled value
    /// </summary>
    private string AriaDisabled => (IsDisabled || IsLoading).ToString().ToLowerInvariant();

    /// <summary>
    /// Clean disposal following .NET 9 patterns
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        AdditionalAttributes.Clear();
        _computedCssClass = string.Empty;
        await base.DisposeAsyncCore();
    }

    #region Parameters

    /// <summary>
    /// The visual style of the button
    /// </summary>
    [Parameter] public ButtonStyle ButtonStyle { get; set; } = ButtonStyle.Solid;

    /// <summary>
    /// The color theme of the button
    /// </summary>
    [Parameter] public ButtonColor Color { get; set; } = ButtonColor.Default;

    /// <summary>
    /// The size of the button
    /// </summary>
    [Parameter] public ButtonSize Size { get; set; } = ButtonSize.Medium;

    /// <summary>
    /// Whether the button should take full width of its container
    /// </summary>
    [Parameter] public bool IsBlock { get; set; }

    /// <summary>
    /// Whether the button is disabled
    /// </summary>
    [Parameter] public bool IsDisabled { get; set; }

    /// <summary>
    /// Whether the button is in a loading state
    /// </summary>
    [Parameter] public bool IsLoading { get; set; }

    /// <summary>
    /// Whether to automatically show loading state when clicked
    /// </summary>
    [Parameter] public bool ShowLoadingOnClick { get; set; }

    /// <summary>
    /// Icon class to display (e.g., FontAwesome, Material Icons)
    /// </summary>
    [Parameter] public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Button type attribute (button, submit, reset)
    /// </summary>
    [Parameter] public string Type { get; set; } = "button";

    /// <summary>
    /// Tooltip text for the button
    /// </summary>
    [Parameter] public string? Tooltip { get; set; }

    /// <summary>
    /// Custom click throttle delay (optional override)
    /// </summary>
    [Parameter] public TimeSpan? ClickThrottleDelay { get; set; }

    /// <summary>
    /// Event callback for button clicks
    /// </summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>
    /// Event callback for handling errors during click processing
    /// </summary>
    [Parameter] public EventCallback<Exception> OnError { get; set; }

    /// <summary>
    /// Child content to render inside the button
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Additional HTML attributes
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    #endregion
}
