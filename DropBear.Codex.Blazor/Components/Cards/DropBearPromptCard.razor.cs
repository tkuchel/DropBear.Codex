#region

using System.Runtime.CompilerServices;
using System.Text;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Cards;

/// <summary>
///     A Blazor component for rendering a prompt card with various styles and options.
///     Optimized for Blazor Server with efficient rendering and memory management.
/// </summary>
public sealed partial class DropBearPromptCard : DropBearComponentBase, IDisposable
{
    // Cache mappings to avoid dictionary lookups in render loops
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
        };

    private static readonly TimeSpan ClickDebounceTime = TimeSpan.FromMilliseconds(100);

    // Button class caching
    private readonly Dictionary<ButtonConfig, string> _buttonClassCache = new();

    // Cache CSS class to avoid rebuilding on each render
    private IReadOnlyCollection<ButtonConfig?> _lastButtons = Array.Empty<ButtonConfig>();

    // Click debounce tracking
    private DateTime _lastClickTime = DateTime.MinValue;
    private string _lastIcon = string.Empty;

    // Track previous parameter values to detect changes
    private PromptType _lastPromptType;
    private bool _lastSubtle;

    // Flag to track parameter changes
    private bool _parametersChanged = true;

    /// <summary>
    ///     Dynamically built CSS classes for the prompt card.
    /// </summary>
    private string CssClass { get; set; } = string.Empty;

    /// <summary>
    ///     Disposes of the component, cleaning up any resources.
    /// </summary>
    public void Dispose()
    {
        try
        {
            // Clear collections to prevent memory leaks
            Buttons = Array.Empty<ButtonConfig>();
            _buttonClassCache.Clear();

            // Clear cached values
            CssClass = string.Empty;
            _lastButtons = Array.Empty<ButtonConfig>();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing prompt card.");
        }
    }

    /// <summary>
    ///     Updates parameters and rebuilds cached values when needed
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // Check if relevant parameters have changed
        var buttonsChanged = _lastButtons.Count != Buttons.Count;
        if (!buttonsChanged && _lastButtons.Count > 0)
        {
            // Deep comparison only when needed
            buttonsChanged = !_lastButtons.SequenceEqual(Buttons, new ButtonConfigComparer());
        }

        // Check for parameter changes
        if (_lastPromptType != PromptType ||
            _lastSubtle != Subtle ||
            _lastIcon != (Icon ?? string.Empty) ||
            buttonsChanged)
        {
            _parametersChanged = true;

            // Update cached values
            _lastPromptType = PromptType;
            _lastSubtle = Subtle;
            _lastIcon = Icon ?? string.Empty;
            _lastButtons = Buttons;

            // Clear button class cache if buttons changed
            if (buttonsChanged)
            {
                _buttonClassCache.Clear();
            }
        }

        ValidateParameters();

        // Rebuild CSS class if needed
        if (_parametersChanged)
        {
            CssClass = BuildPromptCssClass();
            _parametersChanged = false;
        }
    }

    /// <summary>
    ///     Builds the complete CSS class string for the prompt card.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string BuildPromptCssClass()
    {
        var builder = new StringBuilder(100);

        // Base class
        builder.Append("prompt-card");

        // Type class
        string? typeClass;
        if (!PromptClasses.TryGetValue(PromptType, out typeClass))
        {
            typeClass = "prompt-card-default";
        }

        builder.Append(' ');
        builder.Append(typeClass);

        // Subtle variant
        if (Subtle)
        {
            builder.Append(" prompt-card-subtle");
        }

        // Icon indicator
        if (!string.IsNullOrEmpty(Icon))
        {
            builder.Append(" prompt-card-with-icon");
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Returns the appropriate CSS class for a button with efficient caching.
    /// </summary>
    private string GetButtonClass(ButtonConfig? button)
    {
        if (button is null)
        {
            return string.Empty;
        }

        // Use cached value if available
        if (_buttonClassCache.TryGetValue(button, out var cachedClass))
        {
            return cachedClass;
        }

        // Build and cache the class
        var cssClasses = new StringBuilder(40);
        cssClasses.Append("prompt-btn");

        // Add color class
        if (ButtonClasses.TryGetValue(button.Color, out var typeClass))
        {
            cssClasses.Append(' ');
            cssClasses.Append(typeClass);
        }
        else
        {
            cssClasses.Append(" prompt-btn-default");
        }

        // Add icon indicator
        if (!string.IsNullOrEmpty(button.Icon))
        {
            cssClasses.Append(" prompt-btn-with-icon");
        }

        var result = cssClasses.ToString();
        _buttonClassCache[button] = result;
        return result;
    }

    /// <summary>
    ///     Handles the click event for a button in the prompt with debouncing.
    /// </summary>
    private async Task HandleButtonClick(ButtonConfig button)
    {
        if (IsDisposed)
        {
            Logger.Warning("Button click ignored - component is disposed.");
            return;
        }

        // Implement debouncing to prevent accidental double-clicks
        var now = DateTime.UtcNow;
        if (now - _lastClickTime < ClickDebounceTime)
        {
            return;
        }

        _lastClickTime = now;

        if (!OnButtonClicked.HasDelegate)
        {
            Logger.Warning("No click handler provided for button: {ButtonId} {ButtonText}",
                button.Id, button.Text);
            return;
        }

        try
        {
            await QueueStateHasChangedAsync((Func<Task>)(async () =>
            {
                Logger.Debug("Button clicked: {ButtonId} {ButtonText} {ButtonColor}",
                    button.Id, button.Text, button.Color);
                await OnButtonClicked.InvokeAsync(button);
            }));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling button click: {ButtonId} {ButtonText}",
                button.Id, button.Text);
        }
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
            if (string.IsNullOrWhiteSpace(button?.Id) ||
                string.IsNullOrWhiteSpace(button.Text))
            {
                Logger.Warning("Button configuration missing required properties: {ButtonId}",
                    button?.Id ?? "null");
            }
        }
    }

    /// <summary>
    ///     Helper class to compare ButtonConfig objects for equality
    /// </summary>
    private sealed class ButtonConfigComparer : IEqualityComparer<ButtonConfig?>
    {
        public bool Equals(ButtonConfig? x, ButtonConfig? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.Id == y.Id &&
                   x.Text == y.Text &&
                   x.Icon == y.Icon &&
                   x.Color == y.Color;
        }

        public int GetHashCode(ButtonConfig? obj)
        {
            return obj is null
                ? 0
                : HashCode.Combine(obj.Id, obj.Text, obj.Icon, obj.Color);
        }
    }

    #region Parameters

    /// <summary>
    ///     A unique ID for ARIA binding or other needs.
    /// </summary>
    [Parameter]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     An optional icon class for the prompt (e.g., "fas fa-question-circle").
    /// </summary>
    [Parameter]
    public string? Icon { get; set; } = "fas fa-question-circle";

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
    public IReadOnlyCollection<ButtonConfig?> Buttons { get; set; } = Array.Empty<ButtonConfig>();

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
