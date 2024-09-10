#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Configuration model for a button.
/// </summary>
public sealed class ButtonConfig
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ButtonConfig" /> class with default values.
    /// </summary>
    public ButtonConfig() : this(string.Empty, string.Empty, ButtonColor.Default, string.Empty)
    {
        // Default constructor chaining to main constructor
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ButtonConfig" /> class with specified values.
    /// </summary>
    /// <param name="id">The ID of the button.</param>
    /// <param name="text">The text displayed on the button.</param>
    /// <param name="color">The type/color of the button.</param>
    /// <param name="icon">The icon displayed on the button.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="id" /> or <paramref name="text" /> is null or empty.</exception>
    public ButtonConfig(string id, string text, ButtonColor color, string icon)
    {
        Id = string.IsNullOrWhiteSpace(id)
            ? throw new ArgumentException("Button ID cannot be null or empty.", nameof(id))
            : id;

        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Button text cannot be null or empty.", nameof(text))
            : text;

        Color = color;
        Icon = icon ?? string.Empty;
    }

    /// <summary>
    ///     Gets the ID of the button.
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Gets the text displayed on the button.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the type/color of the button.
    /// </summary>
    public ButtonColor Color { get; }

    /// <summary>
    ///     Gets the icon displayed on the button.
    /// </summary>
    public string Icon { get; }
}
