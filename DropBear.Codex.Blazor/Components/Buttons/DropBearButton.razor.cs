#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

/// <summary>
///     A Blazor component for rendering a button with various style, size, and color options.
/// </summary>
public sealed partial class DropBearButton : DropBearComponentBase
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearButton>();

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
    ///     Additional attributes (e.g., data-*, aria-*) to be splatted onto the underlying &lt;button&gt;.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Builds and returns the appropriate CSS class for the button based on its parameters.
    /// </summary>
    private string CssClass => BuildCssClass();

    /// <summary>
    ///     Builds the CSS class for the button based on its style, color, size, and other parameters.
    /// </summary>
    /// <returns>A string representing the final CSS class name(s).</returns>
    private string BuildCssClass()
    {
        var cssClass = "dropbear-btn";

        cssClass += $" dropbear-btn-{ButtonStyle.ToString().ToLowerInvariant()}";
        cssClass += $" dropbear-btn-{Color.ToString().ToLowerInvariant()}";
        cssClass += $" dropbear-btn-{Size.ToString().ToLowerInvariant()}";

        if (IsBlock)
        {
            cssClass += " dropbear-btn-block";
        }

        if (IsDisabled)
        {
            cssClass += " dropbear-btn-disabled";
        }

        if (!string.IsNullOrEmpty(Icon) && ChildContent == null)
        {
            // If there's an icon, but no text/child content, apply a special icon-only style.
            cssClass += " dropbear-btn-icon-only";
        }

        return cssClass.Trim();
    }

    /// <summary>
    ///     Handles the click event. If the button is disabled, it ignores the event. Otherwise, calls <see cref="OnClick" />.
    /// </summary>
    /// <param name="args">The mouse event arguments from the Blazor framework.</param>
    private async Task OnClickHandler(MouseEventArgs args)
    {
        if (IsDisabled)
        {
            Logger.Debug("Button click ignored because the button is disabled.");
            return;
        }

        if (OnClick.HasDelegate)
        {
            try
            {
                Logger.Debug("Button clicked.");
                await OnClick.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during button click handling.");
            }
        }
    }
}
