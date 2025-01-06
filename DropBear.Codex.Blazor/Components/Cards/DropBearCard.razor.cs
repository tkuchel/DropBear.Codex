#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Cards;

/// <summary>
///     A Blazor component for rendering a card with various styles and options.
/// </summary>
public sealed partial class DropBearCard : DropBearComponentBase
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearCard>();

    // Mapping of ButtonColor enum to specific CSS classes
    private static readonly Dictionary<ButtonColor, string> ButtonClasses = new()
    {
        { ButtonColor.Default, "btn-default" },
        { ButtonColor.Secondary, "btn-secondary" },
        { ButtonColor.Success, "btn-success" },
        { ButtonColor.Warning, "btn-warning" },
        { ButtonColor.Error, "btn-error" },
        { ButtonColor.Information, "btn-information" },
        { ButtonColor.Primary, "btn-primary" }
    };

    /// <summary>
    ///     The card variant or style (Default, etc.).
    /// </summary>
    [Parameter]
    public CardType Type { get; set; } = CardType.Default;

    /// <summary>
    ///     If true, applies a more compact layout to the card.
    /// </summary>
    [Parameter]
    public bool CompactMode { get; set; }

    /// <summary>
    ///     The CSS class for controlling a "compact" card.
    ///     Computed based on <see cref="CompactMode" />.
    /// </summary>
    private string CompactClass => CompactMode ? "compact" : string.Empty;

    /// <summary>
    ///     Optional image source to display at the top of the card.
    /// </summary>
    [Parameter]
    public string ImageSource { get; set; } = string.Empty;

    /// <summary>
    ///     Alternate text for the image.
    /// </summary>
    [Parameter]
    public string ImageAlt { get; set; } = string.Empty;

    /// <summary>
    ///     An optional icon class (e.g., FontAwesome) to display in the card header.
    /// </summary>
    [Parameter]
    public string IconSource { get; set; } = string.Empty;

    /// <summary>
    ///     An optional title to display in the card header.
    /// </summary>
    [Parameter]
    public string HeaderTitle { get; set; } = string.Empty;

    /// <summary>
    ///     The card's main body content (render fragment).
    /// </summary>
    [Parameter]
    public RenderFragment? CardBodyContent { get; set; }

    /// <summary>
    ///     If true, allows usage of a custom footer section via <see cref="CardFooterContent" />.
    /// </summary>
    [Parameter]
    public bool UseCustomFooter { get; set; }

    /// <summary>
    ///     When <see cref="UseCustomFooter" /> is true, this render fragment is displayed as the footer.
    /// </summary>
    [Parameter]
    public RenderFragment? CardFooterContent { get; set; }

    /// <summary>
    ///     A collection of button configurations to render in the card footer (if <see cref="UseCustomFooter" /> is false).
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<ButtonConfig> Buttons { get; set; } = Array.Empty<ButtonConfig>();

    /// <summary>
    ///     Event callback invoked when a footer button is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<ButtonConfig> OnButtonClicked { get; set; }

    /// <summary>
    ///     Gets the CSS class associated with a particular button color.
    /// </summary>
    /// <param name="type">The button color.</param>
    private static string GetButtonClass(ButtonColor type)
    {
        return ButtonClasses.GetValueOrDefault(type, "btn-primary");
    }

    /// <summary>
    ///     Handles a button click event and invokes <see cref="OnButtonClicked" />.
    /// </summary>
    /// <param name="button">Button configuration model.</param>
    private async Task HandleButtonClick(ButtonConfig button)
    {
        if (!OnButtonClicked.HasDelegate)
        {
            Logger.Warning("Button click event handler not provided for button: {ButtonText}", button.Text);
            return;
        }

        try
        {
            Logger.Debug("Button clicked: {ButtonText}", button.Text);
            await OnButtonClicked.InvokeAsync(button);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling button click for button: {ButtonText}", button.Text);
        }
    }
}
