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
///     A Blazor component for rendering a button with various styles and options.
/// </summary>
public sealed partial class DropBearButton : DropBearComponentBase
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearButton>();

    [Parameter] public ButtonStyle ButtonStyle { get; set; } = ButtonStyle.Solid;
    [Parameter] public ButtonColor Color { get; set; } = ButtonColor.Default;
    [Parameter] public ButtonSize Size { get; set; } = ButtonSize.Medium;
    [Parameter] public bool IsBlock { get; set; }
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public string Icon { get; set; } = string.Empty;
    [Parameter] public string Type { get; set; } = "button";
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object> AdditionalAttributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private string CssClass => BuildCssClass();

    /// <summary>
    ///     Builds the CSS class for the button based on its properties.
    /// </summary>
    /// <returns>A string representing the CSS class.</returns>
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
            cssClass += " dropbear-btn-icon-only";
        }

        return cssClass.Trim();
    }

    /// <summary>
    ///     Handles the click event for the button.
    /// </summary>
    /// <param name="args">The mouse event arguments.</param>
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
                // Logger.Debug("Button clicked.");
                await OnClick.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during button click handling.");
            }
        }
    }
}
