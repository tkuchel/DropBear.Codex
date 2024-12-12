namespace DropBear.Codex.Blazor.Enums;

/// <summary>
///     Supported easing functions for progress transitions
/// </summary>
public enum EasingFunction
{
    /// <summary>
    ///     Linear progression (no easing)
    /// </summary>
    Linear,

    /// <summary>
    ///     Ease in using quadratic function
    /// </summary>
    EaseInQuad,

    /// <summary>
    ///     Ease out using quadratic function
    /// </summary>
    EaseOutQuad,

    /// <summary>
    ///     Ease in and out using cubic function
    /// </summary>
    EaseInOutCubic,

    /// <summary>
    ///     Ease in using exponential function
    /// </summary>
    EaseInExpo,

    /// <summary>
    ///     Ease out using exponential function
    /// </summary>
    EaseOutExpo
}
