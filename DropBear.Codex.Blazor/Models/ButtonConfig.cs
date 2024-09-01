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
    ///     Initializes a new instance of the <see cref="ButtonConfig" /> class.
    /// </summary>
    public ButtonConfig() { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ButtonConfig" /> class with specified values.
    /// </summary>
    /// <param name="id">The ID of the button.</param>
    /// <param name="text">The text displayed on the button.</param>
    /// <param name="type">The type/color of the button.</param>
    /// <param name="icon">The icon displayed on the button.</param>
    public ButtonConfig(string id, string text, ButtonColor type, string icon)
    {
        Id = id;
        Text = text;
        Type = type;
        Icon = icon;
    }

    /// <summary>
    ///     Gets or sets the ID of the button.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the text displayed on the button.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the type/color of the button.
    /// </summary>
    public ButtonColor Type { get; set; } = ButtonColor.Default;

    /// <summary>
    ///     Gets or sets the icon displayed on the button.
    /// </summary>
    public string Icon { get; set; } = string.Empty;
}
