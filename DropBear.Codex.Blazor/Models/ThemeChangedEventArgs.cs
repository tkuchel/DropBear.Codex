namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Event arguments for theme change events.
/// </summary>
public sealed class ThemeChangedEventArgs : EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ThemeChangedEventArgs"/> class.
    /// </summary>
    /// <param name="theme">The new theme.</param>
    /// <param name="previousTheme">The previous theme.</param>
    /// <param name="userPreference">The user's theme preference.</param>
    /// <param name="animated">Whether the transition was animated.</param>
    public ThemeChangedEventArgs(string theme, string? previousTheme, string userPreference, bool animated)
    {
        Theme = theme;
        PreviousTheme = previousTheme;
        UserPreference = userPreference;
        Animated = animated;
    }

    /// <summary>
    ///     Gets the new theme.
    /// </summary>
    public string Theme { get; }

    /// <summary>
    ///     Gets the previous theme, if any.
    /// </summary>
    public string? PreviousTheme { get; }

    /// <summary>
    ///     Gets the user's theme preference.
    /// </summary>
    public string UserPreference { get; }

    /// <summary>
    ///     Gets a value indicating whether the transition was animated.
    /// </summary>
    public bool Animated { get; }
}
