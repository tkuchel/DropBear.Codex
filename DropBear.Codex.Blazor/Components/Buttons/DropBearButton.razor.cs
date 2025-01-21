#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

/// <summary>
///     A Blazor component for rendering a button with various style, size, and color options.
/// </summary>
public sealed partial class DropBearButton : DropBearComponentBase
{
    /// <summary>
    ///     Constructs a dynamic CSS class based on button parameters.
    /// </summary>
    private string CssClass => BuildCssClass();

    /// <summary>
    ///     Builds the CSS class for the button based on its configuration.
    /// </summary>
    private string BuildCssClass()
    {
        var cssClasses = new List<string>
        {
            "dropbear-btn",
            $"dropbear-btn-{ButtonStyle.ToString().ToLowerInvariant()}",
            $"dropbear-btn-{Color.ToString().ToLowerInvariant()}",
            $"dropbear-btn-{Size.ToString().ToLowerInvariant()}"
        };

        if (IsBlock)
        {
            cssClasses.Add("dropbear-btn-block");
        }

        if (IsDisabled)
        {
            cssClasses.Add("dropbear-btn-disabled");
        }

        if (!string.IsNullOrEmpty(Icon) && ChildContent is null)
        {
            cssClasses.Add("dropbear-btn-icon-only");
        }

        return string.Join(" ", cssClasses);
    }

    /// <summary>
    ///     Handles the button click event, respecting the disabled state.
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

        if (!OnClick.HasDelegate)
        {
            return;
        }

        try
        {
            await InvokeStateHasChangedAsync(async () =>
            {
                Logger.Debug("Button clicked: {Color} {ButtonStyle} {Size}", Color, ButtonStyle, Size);
                await OnClick.InvokeAsync(args);
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling button click: {Color} {ButtonStyle}", Color, ButtonStyle);
        }
    }

    /// <summary>
    ///     Disposes of the component, cleaning up any resources.
    ///     This method is called by the Blazor framework when the component is removed from the UI.
    /// </summary>
    public void Dispose()
    {
        // Clean up any event handlers or resources if needed
        AdditionalAttributes.Clear();
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
