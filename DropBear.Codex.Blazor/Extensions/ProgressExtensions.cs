using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;

namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
///     Extension methods for progress-related functionality.
/// </summary>
public static class ProgressExtensions
{
    /// <summary>
    ///     Converts a StepStatus to a CSS class name.
    /// </summary>
    /// <param name="status">The step status.</param>
    /// <returns>A CSS class name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCssClass(this StepStatus status)
    {
        return status switch
        {
            StepStatus.NotStarted => "not-started",
            StepStatus.InProgress => "in-progress",
            StepStatus.Completed => "completed",
            StepStatus.Warning => "warning",
            StepStatus.Failed => "failed",
            StepStatus.Skipped => "skipped",
            _ => "unknown"
        };
    }

    /// <summary>
    ///     Converts a StepPosition to a CSS class name.
    /// </summary>
    /// <param name="position">The step position.</param>
    /// <returns>A CSS class name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToCssClass(this StepPosition position)
    {
        return position switch
        {
            StepPosition.Previous => "step-previous",
            StepPosition.Current => "step-current",
            StepPosition.Next => "step-next",
            _ => string.Empty
        };
    }

    /// <summary>
    ///     Determines if a status represents a completed state.
    /// </summary>
    /// <param name="status">The step status.</param>
    /// <returns>True if the status is completed, warning, failed, or skipped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinished(this StepStatus status)
    {
        return status is StepStatus.Completed or StepStatus.Warning or StepStatus.Failed or StepStatus.Skipped;
    }

    /// <summary>
    ///     Determines if a status represents an active state.
    /// </summary>
    /// <param name="status">The step status.</param>
    /// <returns>True if the status is in progress.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsActive(this StepStatus status)
    {
        return status == StepStatus.InProgress;
    }

    /// <summary>
    ///     Determines if a status represents an error state.
    /// </summary>
    /// <param name="status">The step status.</param>
    /// <returns>True if the status is failed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsError(this StepStatus status)
    {
        return status == StepStatus.Failed;
    }

    /// <summary>
    ///     Determines if a status represents a success state.
    /// </summary>
    /// <param name="status">The step status.</param>
    /// <returns>True if the status is completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSuccess(this StepStatus status)
    {
        return status == StepStatus.Completed;
    }

    /// <summary>
    ///     Gets the severity level of a status for sorting/prioritization.
    /// </summary>
    /// <param name="status">The step status.</param>
    /// <returns>A severity level (higher = more severe).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSeverityLevel(this StepStatus status)
    {
        return status switch
        {
            StepStatus.Failed => 5,
            StepStatus.Warning => 4,
            StepStatus.InProgress => 3,
            StepStatus.Completed => 2,
            StepStatus.Skipped => 1,
            StepStatus.NotStarted => 0,
            _ => 0
        };
    }

    /// <summary>
    ///     Clamps a progress value to the valid range (0-100).
    /// </summary>
    /// <param name="progress">The progress value to clamp.</param>
    /// <returns>A clamped progress value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ClampProgress(this double progress)
    {
        return Math.Clamp(progress, 0.0, 100.0);
    }

    /// <summary>
    ///     Formats a progress value as a percentage string.
    /// </summary>
    /// <param name="progress">The progress value (0-100).</param>
    /// <param name="decimals">Number of decimal places.</param>
    /// <returns>A formatted percentage string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToPercentageString(this double progress, int decimals = 0)
    {
        return $"{progress.ClampProgress().ToString($"F{decimals}")}%";
    }
}
