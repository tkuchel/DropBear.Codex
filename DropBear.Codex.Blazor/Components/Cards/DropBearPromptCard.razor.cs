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
///     A Blazor component for rendering a prompt card with various styles and options.
/// </summary>
public sealed partial class DropBearPromptCard : DropBearComponentBase
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearPromptCard>();

    private static readonly Dictionary<ButtonColor, string> ButtonClasses = new()
    {
        { ButtonColor.Primary, "prompt-btn-primary" },
        { ButtonColor.Secondary, "prompt-btn-secondary" },
        { ButtonColor.Success, "prompt-btn-success" },
        { ButtonColor.Warning, "prompt-btn-warning" },
        { ButtonColor.Error, "prompt-btn-danger" },
        { ButtonColor.Default, "prompt-btn-default" }
    };

    [Parameter] public string Icon { get; set; } = "fas fa-question-circle";
    [Parameter] public string Title { get; set; } = "Title";
    [Parameter] public string Description { get; set; } = "Description";
    [Parameter] public IReadOnlyCollection<ButtonConfig> Buttons { get; set; } = Array.Empty<ButtonConfig>();
    [Parameter] public EventCallback<ButtonConfig> OnButtonClicked { get; set; }
    [Parameter] public PromptType PromptType { get; set; } = PromptType.Information;
    [Parameter] public bool Subtle { get; set; }

    /// <summary>
    ///     Gets the CSS class for the specified button color.
    /// </summary>
    /// <param name="type">The button color.</param>
    /// <returns>A string representing the CSS class.</returns>
    private string GetButtonClass(ButtonColor type)
    {
        const string BaseClass = "prompt-btn";
        var typeClass = ButtonClasses.GetValueOrDefault(type, "prompt-btn-default");

        // Construct the full CSS class for the button
        return $"{BaseClass} {typeClass}".Trim();
    }

    /// <summary>
    ///     Returns the CSS class for the prompt card based on its type.
    /// </summary>
    private string GetPromptClass()
    {
        return PromptType switch
        {
            PromptType.Success => "prompt-card-success",
            PromptType.Warning => "prompt-card-warning",
            PromptType.Error => "prompt-card-danger",
            PromptType.Information => "prompt-card-information",
            _ => "prompt-card-default"
        };
    }

    /// <summary>
    ///     Handles the button click event.
    /// </summary>
    /// <param name="button">The button configuration.</param>
    private async Task OnButtonClick(ButtonConfig button)
    {
        try
        {
            Logger.Information("Button clicked: {ButtonText}", button.Text);
            await OnButtonClicked.InvokeAsync(button);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while handling button click for button: {ButtonText}", button.Text);
        }
    }
}
