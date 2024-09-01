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

    [Parameter] public CardType Type { get; set; } = CardType.Default;

    [Parameter] public bool CompactMode { get; set; }
    private string CompactSelected => CompactMode ? "compact" : string.Empty;

    [Parameter] public string ImageSource { get; set; } = string.Empty;
    [Parameter] public string ImageAlt { get; set; } = string.Empty;

    [Parameter] public string IconSource { get; set; } = string.Empty;

    [Parameter] public string HeaderTitle { get; set; } = string.Empty;

    [Parameter] public RenderFragment? CardBodyContent { get; set; }

    [Parameter] public bool UseCustomFooter { get; set; }
    [Parameter] public RenderFragment? CardFooterContent { get; set; }

    [Parameter] public IReadOnlyCollection<ButtonConfig> Buttons { get; set; } = Array.Empty<ButtonConfig>();

    [Parameter] public EventCallback<ButtonConfig> OnButtonClicked { get; set; }

    /// <summary>
    ///     Gets the CSS class for the specified button color.
    /// </summary>
    /// <param name="type">The button color.</param>
    /// <returns>A string representing the CSS class.</returns>
    private static string GetButtonClass(ButtonColor type)
    {
        return ButtonClasses.GetValueOrDefault(type, "btn-primary");
    }

    /// <summary>
    ///     Handles the button click event.
    /// </summary>
    /// <param name="button">The button configuration.</param>
    private async Task OnButtonClick(ButtonConfig button)
    {
        await OnButtonClicked.InvokeAsync(button);
    }
}
