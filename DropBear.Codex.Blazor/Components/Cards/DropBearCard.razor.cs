#region

using System.Text;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Cards;

/// <summary>
///     A Blazor component for rendering a card with various styles and options.
///     Optimized for Blazor Server with efficient rendering and memory management.
/// </summary>
public sealed partial class DropBearCard : DropBearComponentBase, IDisposable
{
    // Cache button mappings to avoid dictionary lookups
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

    // Unique ID for this card instance
    private readonly string _cardId = $"card-{Guid.NewGuid():N}";

    // Cache CSS class to avoid rebuilding on each render
    private bool _lastCompactMode;
    private string _lastIconSource = string.Empty;
    private string _lastImageSource = string.Empty;

    // Track previous parameter values to detect changes
    private CardType _lastType;

    // Flag to track if parameters have changed
    private bool _parametersChanged = true;

    /// <summary>
    ///     Gets the CSS class based on card configuration.
    /// </summary>
    private string CssClass { get; set; } = string.Empty;

    /// <summary>
    ///     Disposes of the component, cleaning up any resources.
    /// </summary>
    public void Dispose()
    {
        try
        {
            // Clear references to prevent memory leaks
            Buttons = [];

            // Clear cached values
            CssClass = string.Empty;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing DropBearCard");
        }
    }

    /// <summary>
    ///     Updates parameters and cache when they change
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // Check if relevant parameters have changed
        if (_lastType != Type ||
            _lastCompactMode != CompactMode ||
            _lastImageSource != ImageSource ||
            _lastIconSource != IconSource)
        {
            _parametersChanged = true;

            // Update cached values
            _lastType = Type;
            _lastCompactMode = CompactMode;
            _lastImageSource = ImageSource ?? string.Empty;
            _lastIconSource = IconSource ?? string.Empty;
        }

        // Validate image configuration
        ValidateImageConfig();

        // Rebuild CSS class if parameters changed
        if (_parametersChanged)
        {
            CssClass = BuildCssClass();
            _parametersChanged = false;
        }
    }

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
            await QueueStateHasChangedAsync((Func<Task>)(async () =>
            {
                Logger.Debug("Button clicked: {ButtonText}", button.Text);
                await OnButtonClicked.InvokeAsync(button);
            }));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling button click: {ButtonText}", button.Text);
        }
    }

    /// <summary>
    ///     Gets the CSS class for a button type with efficient caching.
    /// </summary>
    private static string GetButtonClass(ButtonColor type)
    {
        return ButtonClasses.TryGetValue(type, out var cssClass)
            ? cssClass
            : ButtonClasses[ButtonColor.Primary];
    }

    /// <summary>
    ///     Builds CSS classes for the card with efficient string concatenation.
    /// </summary>
    private string BuildCssClass()
    {
        var stringBuilder = new StringBuilder(100);

        // Base class
        stringBuilder.Append("dropbear-card");

        // Type variant
        stringBuilder.Append(" dropbear-card-");
        stringBuilder.Append(Type.ToString().ToLowerInvariant());

        // Compact mode
        if (CompactMode)
        {
            stringBuilder.Append(" compact");
        }

        // Image indicator
        if (!string.IsNullOrEmpty(ImageSource))
        {
            stringBuilder.Append(" dropbear-card-with-image");
        }

        // Icon indicator
        if (!string.IsNullOrEmpty(IconSource))
        {
            stringBuilder.Append(" dropbear-card-with-icon");
        }

        return stringBuilder.ToString();
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
    public string? ImageSource { get; set; }

    /// <summary>
    ///     Alternate text for the image.
    /// </summary>
    [Parameter]
    public string ImageAlt { get; set; } = string.Empty;

    /// <summary>
    ///     An optional icon class to display in the card header.
    /// </summary>
    [Parameter]
    public string? IconSource { get; set; }

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
    public IReadOnlyCollection<ButtonConfig> Buttons { get; set; } = [];

    /// <summary>
    ///     Event callback invoked when a footer button is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<ButtonConfig> OnButtonClicked { get; set; }

    #endregion
}
