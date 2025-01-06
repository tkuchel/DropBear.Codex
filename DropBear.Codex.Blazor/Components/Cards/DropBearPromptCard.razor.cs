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
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearPromptCard>();

    // Button color -> CSS class mapping
    private static readonly Dictionary<ButtonColor, string> ButtonClasses = new()
    {
        { ButtonColor.Primary, "prompt-btn-primary" },
        { ButtonColor.Secondary, "prompt-btn-secondary" },
        { ButtonColor.Success, "prompt-btn-success" },
        { ButtonColor.Warning, "prompt-btn-warning" },
        { ButtonColor.Error, "prompt-btn-danger" },
        { ButtonColor.Default, "prompt-btn-default" }
    };

    /// <summary>
    ///     An optional icon class for the prompt (e.g., "fas fa-question-circle").
    /// </summary>
    [Parameter]
    public string Icon { get; set; } = "fas fa-question-circle";

    /// <summary>
    ///     The title text displayed in the prompt header.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = "Title";

    /// <summary>
    ///     The main descriptive text displayed in the body of the prompt.
    /// </summary>
    [Parameter]
    public string Description { get; set; } = "Description";

    /// <summary>
    ///     The collection of button configurations to display in the prompt's footer.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<ButtonConfig> Buttons { get; set; } = Array.Empty<ButtonConfig>();

    /// <summary>
    ///     Event callback for when a button is clicked in the prompt.
    /// </summary>
    [Parameter]
    public EventCallback<ButtonConfig> OnButtonClicked { get; set; }

    /// <summary>
    ///     The type of prompt (e.g., Success, Warning, Error, Information).
    /// </summary>
    [Parameter]
    public PromptType PromptType { get; set; } = PromptType.Information;

    /// <summary>
    ///     If true, applies a more subdued style to the prompt (e.g., minimal color).
    /// </summary>
    [Parameter]
    public bool Subtle { get; set; }

    /// <summary>
    ///     Returns the appropriate CSS class for a <see cref="ButtonColor" />.
    /// </summary>
    /// <param name="type">The button color.</param>
    /// <returns>A CSS class name.</returns>
    private string GetButtonClass(ButtonColor type)
    {
        const string baseClass = "prompt-btn";
        var typeClass = ButtonClasses.GetValueOrDefault(type, "prompt-btn-default");

        // Combine the base class with the color class
        return $"{baseClass} {typeClass}".Trim();
    }

    /// <summary>
    ///     Returns the CSS class that corresponds to the current <see cref="PromptType" />.
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
    ///     Handles the click event for a button in the prompt.
    /// </summary>
    /// <param name="button">The <see cref="ButtonConfig" /> model representing the button.</param>
    private async Task HandleButtonClick(ButtonConfig button)
    {
        if (!OnButtonClicked.HasDelegate)
        {
            Logger.Warning("No button click event handler provided for button: {ButtonText}", button.Text);
            return;
        }

        try
        {
            Logger.Debug("Button clicked: {ButtonText}", button.Text);
            await OnButtonClicked.InvokeAsync(button);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error occurred while handling button click for button: {ButtonText}", button.Text);
        }
    }
}
