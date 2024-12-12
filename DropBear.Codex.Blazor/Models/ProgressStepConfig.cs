#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Configuration for a progress step in the DropBearProgressBar
/// </summary>
public sealed class ProgressStepConfig
{
    /// <summary>
    ///     Gets or sets the unique identifier for this step
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Gets or sets the display name of the step
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Gets or sets the tooltip text for this step
    /// </summary>
    public string? Tooltip { get; set; }

    /// <summary>
    ///     Gets or sets the minimum display time for this step in milliseconds
    /// </summary>
    public int MinimumDisplayTimeMs { get; set; } = 500;

    /// <summary>
    ///     Gets or sets whether this step should show immediate progress or use smooth transitions
    /// </summary>
    public bool UseSmoothProgress { get; set; } = true;

    /// <summary>
    ///     Gets or sets the easing function to use for progress transitions
    /// </summary>
    public EasingFunction EasingFunction { get; set; } = EasingFunction.EaseInOutCubic;
}
