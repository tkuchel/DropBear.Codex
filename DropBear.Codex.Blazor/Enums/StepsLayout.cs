namespace DropBear.Codex.Blazor.Enums;

/// <summary>
///     Layout options for progress steps.
/// </summary>
public enum StepsLayout
{
    /// <summary>
    ///     Steps displayed in a responsive horizontal grid.
    /// </summary>
    Horizontal,

    /// <summary>
    ///     Steps displayed in a single vertical column.
    /// </summary>
    Vertical,

    /// <summary>
    ///     Steps displayed in a compact grid with smaller sizing.
    /// </summary>
    Compact,

    /// <summary>
    ///     Steps displayed in a horizontal scrollable timeline. Best for many steps (6+).
    /// </summary>
    Timeline,

    /// <summary>
    ///     Steps displayed in an ultra-compact wrapped layout. Best for very many steps (10+).
    /// </summary>
    Dense,

    /// <summary>
    ///     Automatically selects the best layout based on step count.
    ///     Uses Horizontal for 1-4 steps, Compact for 5-6, Timeline for 7-10, Dense for 11+.
    /// </summary>
    Auto
}
