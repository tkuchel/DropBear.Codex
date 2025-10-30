namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Contains information about the current theme state.
/// </summary>
public sealed class ThemeInfo
{
    /// <summary>
    ///     Gets or sets the currently active theme.
    /// </summary>
    public required string Current { get; init; }

    /// <summary>
    ///     Gets or sets the effective theme (resolved from auto).
    /// </summary>
    public required string Effective { get; init; }

    /// <summary>
    ///     Gets or sets the user's theme preference.
    /// </summary>
    public required string UserPreference { get; init; }

    /// <summary>
    ///     Gets or sets the system theme preference.
    /// </summary>
    public required string SystemTheme { get; init; }

    /// <summary>
    ///     Gets or sets a value indicating whether the theme is set to auto.
    /// </summary>
    public required bool IsAuto { get; init; }

    /// <summary>
    ///     Gets or sets a value indicating whether the current theme is dark.
    /// </summary>
    public required bool IsDark { get; init; }

    /// <summary>
    ///     Gets or sets a value indicating whether the current theme is light.
    /// </summary>
    public required bool IsLight { get; init; }

    /// <summary>
    ///     Creates a new instance of <see cref="ThemeInfo"/> from a dictionary.
    /// </summary>
    /// <param name="data">Dictionary containing theme information.</param>
    /// <returns>A new ThemeInfo instance.</returns>
    public static ThemeInfo FromDictionary(Dictionary<string, object> data)
    {
        return new ThemeInfo
        {
            Current = data.GetValueOrDefault("current")?.ToString() ?? "light",
            Effective = data.GetValueOrDefault("effective")?.ToString() ?? "light",
            UserPreference = data.GetValueOrDefault("userPreference")?.ToString() ?? "auto",
            SystemTheme = data.GetValueOrDefault("systemTheme")?.ToString() ?? "light",
            IsAuto = data.GetValueOrDefault("isAuto") as bool? ?? false,
            IsDark = data.GetValueOrDefault("isDark") as bool? ?? false,
            IsLight = data.GetValueOrDefault("isLight") as bool? ?? true
        };
    }
}
