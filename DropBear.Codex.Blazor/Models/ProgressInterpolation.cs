﻿#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Provides smooth progress interpolation and easing functions for the progress bar
/// </summary>
internal static class ProgressInterpolation
{
    private const double Precision = 0.001;
    private const int MaxIterations = 1000;

    /// <summary>
    ///     Calculates a smoothed progress value based on the easing function
    /// </summary>
    /// <param name="start">Starting progress value</param>
    /// <param name="end">Target progress value</param>
    /// <param name="elapsedTime">Time elapsed since start of transition</param>
    /// <param name="duration">Total duration of transition</param>
    /// <param name="easing">Easing function to use</param>
    /// <returns>Interpolated progress value</returns>
    public static double InterpolateProgress(
        double start,
        double end,
        TimeSpan elapsedTime,
        TimeSpan duration,
        EasingFunction easing)
    {
        var t = Math.Clamp(elapsedTime.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
        var easedT = ApplyEasing(t, easing);
        return start + ((end - start) * easedT);
    }

    /// <summary>
    ///     Applies the specified easing function to a time value
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ApplyEasing(double t, EasingFunction easing)
    {
        return easing switch
        {
            EasingFunction.Linear => t,
            EasingFunction.EaseInQuad => t * t,
            EasingFunction.EaseOutQuad => t * (2 - t),
            EasingFunction.EaseInOutCubic => t < 0.5 ? 4 * t * t * t : ((t - 1) * ((2 * t) - 2) * ((2 * t) - 2)) + 1,
            EasingFunction.EaseInExpo => t == 0 ? 0 : Math.Pow(2, 10 * (t - 1)),
            EasingFunction.EaseOutExpo => t == 1 ? 1 : 1 - Math.Pow(2, -10 * t),
            _ => t
        };
    }

    /// <summary>
    ///     Generates a sequence of progress values for smooth animation
    /// </summary>
    public static async IAsyncEnumerable<double> GenerateProgressSequence(
        double start,
        double end,
        TimeSpan duration,
        EasingFunction easing,
        int fps = 60,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var frameTime = TimeSpan.FromSeconds(1.0 / fps);
        var elapsedTime = TimeSpan.Zero;
        var iterations = 0;
        var lastValue = start;

        while (elapsedTime < duration && iterations++ < MaxIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentValue = InterpolateProgress(start, end, elapsedTime, duration, easing);

            // Only yield if there's a meaningful change
            if (Math.Abs(currentValue - lastValue) >= Precision)
            {
                yield return currentValue;
                lastValue = currentValue;
            }

            await Task.Delay(frameTime, cancellationToken).ConfigureAwait(false);
            elapsedTime += frameTime;
        }

        // Ensure we end exactly at the target value
        if (Math.Abs(lastValue - end) >= Precision)
        {
            yield return end;
        }
    }
}
