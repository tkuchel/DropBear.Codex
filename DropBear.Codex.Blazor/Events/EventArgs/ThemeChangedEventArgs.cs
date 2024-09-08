namespace DropBear.Codex.Blazor.Events.EventArgs;

public class ThemeChangedEventArgs : System.EventArgs
{
    public ThemeChangedEventArgs(string effectiveTheme, string userPreference)
    {
        EffectiveTheme = effectiveTheme;
        UserPreference = userPreference;
    }

    public string EffectiveTheme { get; }
    public string UserPreference { get; }
}
