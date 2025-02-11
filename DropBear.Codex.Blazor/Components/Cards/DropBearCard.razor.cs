#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Cards;

/// <summary>
///     A Blazor component for rendering a card with various styles and options.
/// </summary>
public sealed partial class DropBearCard : DropBearComponentBase
{
    private static readonly IReadOnlyDictionary<ButtonColor, string> ButtonClasses = new Dictionary<ButtonColor, string>
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
    ///     The CSS class for controlling a "compact" card.
    /// </summary>
    private string CompactClass => CompactMode ? "compact" : string.Empty;

    /// <summary>
    ///     Gets the CSS class based on card configuration.
    /// </summary>
    private string CssClass => BuildCssClass();

    /// <summary>
    ///     Handles a button click event and invokes OnButtonClicked.
    /// </summary>
    private async Task HandleButtonClick(ButtonConfig button)
    {
        if (IsDisposed)
        {
            Logger.Warning("Button click ignored - component is disposed");
            return;
        }

        if (!OnButtonClicked.HasDelegate)
        {
            Logger.Warning("No button click handler provided for: {ButtonText}", button.Text);
            return;
        }

        try
        {
            await QueueStateHasChangedAsync(async () =>
            {
                Logger.Debug("Button clicked: {ButtonText}", button.Text);
                await OnButtonClicked.InvokeAsync(button);
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling button click: {ButtonText}", button.Text);
        }
    }

    /// <summary>
    ///     Gets the CSS class for a button type.
    /// </summary>
    private static string GetButtonClass(ButtonColor type)
    {
        return ButtonClasses.GetValueOrDefault(type, ButtonClasses[ButtonColor.Primary]);
    }

    /// <summary>
    ///     Builds CSS classes for the card.
    /// </summary>
    private string BuildCssClass()
    {
        var cssClasses = new List<string> { "dropbear-card", $"dropbear-card-{Type.ToString().ToLowerInvariant()}" };

        if (CompactMode)
        {
            cssClasses.Add("compact");
        }

        if (!string.IsNullOrEmpty(ImageSource))
        {
            cssClasses.Add("dropbear-card-with-image");
        }

        if (!string.IsNullOrEmpty(IconSource))
        {
            cssClasses.Add("dropbear-card-with-icon");
        }

        return string.Join(" ", cssClasses);
    }

    /// <summary>
    ///     Validates the image configuration.
    /// </summary>
    private void ValidateImageConfig()
    {
        if (!string.IsNullOrEmpty(ImageSource) && string.IsNullOrEmpty(ImageAlt))
        {
            Logger.Warning("Image source provided without alt text: {ImageSource}", ImageSource);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ValidateImageConfig();
    }

    /// <summary>
    ///     Disposes of the component, cleaning up any resources.
    ///     This method is called by the Blazor framework when the component is removed from the UI.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Buttons = Array.Empty<ButtonConfig>();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing DropBearCard");
        }
    }

    #region Parameters

    /// <summary>
    ///     The card variant or style.
    /// </summary>
    [Parameter]
    public CardType Type { get; set; } = CardType.Default;

    /// <summary>
    ///     If true, applies a more compact layout to the card.
    /// </summary>
    [Parameter]
    public bool CompactMode { get; set; }

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
    ///     An optional icon class to display in the card header.
    /// </summary>
    [Parameter]
    public string IconSource { get; set; } = string.Empty;

    /// <summary>
    ///     An optional title to display in the card header.
    /// </summary>
    [Parameter]
    public string HeaderTitle { get; set; } = string.Empty;

    /// <summary>
    ///     The card's main body content.
    /// </summary>
    [Parameter]
    public RenderFragment? CardBodyContent { get; set; }

    /// <summary>
    ///     If true, allows usage of a custom footer section.
    /// </summary>
    [Parameter]
    public bool UseCustomFooter { get; set; }

    /// <summary>
    ///     Custom footer content when UseCustomFooter is true.
    /// </summary>
    [Parameter]
    public RenderFragment? CardFooterContent { get; set; }

    /// <summary>
    ///     Button configurations to render in the default footer.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<ButtonConfig> Buttons { get; set; } = Array.Empty<ButtonConfig>();

    /// <summary>
    ///     Event callback invoked when a footer button is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<ButtonConfig> OnButtonClicked { get; set; }

    #endregion
}
