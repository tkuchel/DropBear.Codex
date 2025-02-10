#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Cards;

/// <summary>
///     A Blazor component for rendering a prompt card with various styles and options.
/// </summary>
public sealed partial class DropBearPromptCard : DropBearComponentBase, IDisposable
{
    // Mappings
    private static readonly IReadOnlyDictionary<ButtonColor, string> ButtonClasses =
        new Dictionary<ButtonColor, string>
        {
            { ButtonColor.Primary, "prompt-btn-primary" },
            { ButtonColor.Secondary, "prompt-btn-secondary" },
            { ButtonColor.Success, "prompt-btn-success" },
            { ButtonColor.Warning, "prompt-btn-warning" },
            { ButtonColor.Error, "prompt-btn-danger" },
            { ButtonColor.Default, "prompt-btn-default" }
        };

    private static readonly IReadOnlyDictionary<PromptType, string> PromptClasses =
        new Dictionary<PromptType, string>
        {
            { PromptType.Success, "prompt-card-success" },
            { PromptType.Warning, "prompt-card-warning" },
            { PromptType.Error, "prompt-card-danger" },
            { PromptType.Information, "prompt-card-information" }
            // Potentially add a Subtle or default here if you like
        };

    /// <summary>
    ///     Dynamically built CSS classes for the prompt card.
    /// </summary>
    private string CssClass => BuildPromptCssClass();

    /// <summary>
    ///     Disposes of the component, cleaning up any resources.
    /// </summary>
    public void Dispose()
    {
        // If there's no real resource to free, disposal might be optional.
        try
        {
            // Example usage: Clear references to avoid potential memory leaks
            Buttons = Array.Empty<ButtonConfig>();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing prompt card.");
        }
    }

    /// <summary>
    ///     Builds the complete CSS class string for the prompt card.
    /// </summary>
    private string BuildPromptCssClass()
    {
        var cssClasses = new List<string>
        {
            "prompt-card", PromptClasses.GetValueOrDefault(PromptType, "prompt-card-default")
        };

        if (Subtle)
        {
            cssClasses.Add("prompt-card-subtle");
        }

        // If the consumer wants to hide the icon, they can set Icon=null or empty
        if (!string.IsNullOrEmpty(Icon))
        {
            cssClasses.Add("prompt-card-with-icon");
        }

        return string.Join(" ", cssClasses);
    }

    /// <summary>
    ///     Returns the appropriate CSS class for a button.
    /// </summary>
    private string GetButtonClass(ButtonConfig button)
    {
        if (button is null)
        {
            return string.Empty;
        }

        var cssClasses = new List<string> { "prompt-btn" };

        var typeClass = ButtonClasses.GetValueOrDefault(button.Color, "prompt-btn-default");
        cssClasses.Add(typeClass);

        if (!string.IsNullOrEmpty(button.Icon))
        {
            cssClasses.Add("prompt-btn-with-icon");
        }

        return string.Join(" ", cssClasses);
    }

    /// <summary>
    ///     Handles the click event for a button in the prompt.
    /// </summary>
    private async Task HandleButtonClick(ButtonConfig button)
    {
        if (IsDisposed)
        {
            Logger.Warning("Button click ignored - component is disposed.");
            return;
        }

        if (!OnButtonClicked.HasDelegate)
        {
            Logger.Warning("No click handler provided for button: {ButtonId} {ButtonText}",
                button.Id, button.Text);
            return;
        }

        try
        {
            await QueueStateHasChangedAsync(async () =>
            {
                Logger.Debug("Button clicked: {ButtonId} {ButtonText} {ButtonColor}",
                    button.Id, button.Text, button.Color);
                await OnButtonClicked.InvokeAsync(button);
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling button click: {ButtonId} {ButtonText}",
                button.Id, button.Text);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ValidateParameters();
    }

    /// <summary>
    ///     Validates the component parameters.
    /// </summary>
    private void ValidateParameters()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            Logger.Warning("Prompt card created without title.");
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            Logger.Warning("Prompt card created without description.");
        }

        foreach (var button in Buttons)
        {
            if (string.IsNullOrWhiteSpace(button.Id) ||
                string.IsNullOrWhiteSpace(button.Text))
            {
                Logger.Warning("Button configuration missing required properties: {ButtonId}",
                    button.Id ?? "null");
            }
        }
    }

    #region Parameters

    /// <summary>
    ///     A unique ID for ARIA binding or other needs.
    /// </summary>
    [Parameter]
    public string Id { get; set; } = Guid.NewGuid().ToString();

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
    ///     The main descriptive text / message displayed in the body of the prompt.
    /// </summary>
    [Parameter]
    public string Message { get; set; } = "Message";

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
    ///     If true, applies a more subdued style to the prompt.
    /// </summary>
    [Parameter]
    public bool Subtle { get; set; }

    #endregion
}
