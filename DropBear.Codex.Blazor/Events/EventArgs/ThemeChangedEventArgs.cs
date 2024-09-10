namespace DropBear.Codex.Blazor.Events.EventArgs;

/// <summary>
///     Provides data for the theme changed event.
/// </summary>
public class ThemeChangedEventArgs : System.EventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ThemeChangedEventArgs" /> class.
    /// </summary>
    /// <param name="effectiveTheme">The currently active theme.</param>
    /// <param name="userPreference">The user's preferred theme, if any.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="effectiveTheme" /> or
    ///     <paramref name="userPreference" /> is null.
    /// </exception>
    public ThemeChangedEventArgs(string effectiveTheme, string userPreference)
    {
        EffectiveTheme = effectiveTheme ?? throw new ArgumentNullException(nameof(effectiveTheme));
        UserPreference = userPreference ?? throw new ArgumentNullException(nameof(userPreference));
    }

    /// <summary>
    ///     Gets the currently active theme after the change.
    /// </summary>
    public string EffectiveTheme { get; }

    /// <summary>
    ///     Gets the user's preferred theme.
    /// </summary>
    public string UserPreference { get; }
}
