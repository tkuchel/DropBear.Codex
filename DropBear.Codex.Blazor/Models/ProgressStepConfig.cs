#region

using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Configuration for a single progress step in a multistep progress bar.
/// </summary>
public abstract class ProgressStepConfig
{
    /// <summary>
    ///     Gets or sets the unique identifier for this step.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Gets or sets the display name of the step.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Gets or sets the tooltip text for this step, shown on hover (if supported).
    /// </summary>
    public string? Tooltip { get; set; }

    /// <summary>
    ///     Gets or sets the minimum time (in milliseconds) that this step should be displayed
    ///     before moving on to the next step. Default is 500ms.
    /// </summary>
    public int MinimumDisplayTimeMs { get; set; } = 500;

    /// <summary>
    ///     Gets or sets a value indicating whether this step's progress
    ///     should animate smoothly or update immediately.
    /// </summary>
    public bool UseSmoothProgress { get; set; } = true;

    /// <summary>
    ///     Gets or sets the easing function used when <see cref="UseSmoothProgress" /> is true.
    /// </summary>
    public EasingFunction EasingFunction { get; set; } = EasingFunction.EaseInOutCubic;
}
