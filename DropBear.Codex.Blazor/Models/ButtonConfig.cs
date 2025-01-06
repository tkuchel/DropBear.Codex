#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models
{
    /// <summary>
    ///     Configuration model for a button, including text, color, and an optional icon.
    /// </summary>
    public sealed class ButtonConfig
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ButtonConfig"/> class with default values.
        ///     Internally chains to the main constructor with empty strings and <see cref="ButtonColor.Default"/>.
        /// </summary>
        public ButtonConfig()
            : this(string.Empty, string.Empty, ButtonColor.Default, string.Empty)
        {
            // Default constructor
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ButtonConfig"/> class with specified values.
        /// </summary>
        /// <param name="id">The unique identifier for the button.</param>
        /// <param name="text">The text displayed on the button.</param>
        /// <param name="color">The color (style) of the button.</param>
        /// <param name="icon">An optional icon displayed on the button (empty if not provided).</param>
        /// <exception cref="ArgumentException">
        ///     Thrown if <paramref name="id"/> or <paramref name="text"/> is null or empty.
        /// </exception>
        public ButtonConfig(string id, string text, ButtonColor color, string? icon)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Button ID cannot be null or empty.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Button text cannot be null or empty.", nameof(text));
            }

            Id = id;
            Text = text;
            Color = color;
            Icon = icon ?? string.Empty;
        }

        /// <summary>
        ///     Gets the unique identifier for the button.
        /// </summary>
        public string Id { get; }

        /// <summary>
        ///     Gets the text displayed on the button.
        /// </summary>
        public string Text { get; }

        /// <summary>
        ///     Gets the color (style) of the button.
        /// </summary>
        public ButtonColor Color { get; }

        /// <summary>
        ///     Gets the icon displayed on the button (if any).
        ///     Returns an empty string if no icon is set.
        /// </summary>
        public string Icon { get; }
    }
}
