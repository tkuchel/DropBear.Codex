#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Provides smooth progress interpolation and easing functions for the progress bar.
///     This class is internal and should only be used within the Blazor library.
/// </summary>
internal static class ProgressInterpolation
{
    private const double Precision = 0.001;
    private const int MaxIterations = 1000;

    /// <summary>
    ///     Calculates a smoothed progress value from <paramref name="start" /> to <paramref name="end" />
    ///     based on the given <paramref name="easing" /> function and elapsed time.
    /// </summary>
    /// <param name="start">The starting progress value.</param>
    /// <param name="end">The target progress value.</param>
    /// <param name="elapsedTime">The time elapsed since the start of the transition.</param>
    /// <param name="duration">The total duration of the transition.</param>
    /// <param name="easing">The easing function to apply for interpolation.</param>
    /// <returns>The interpolated progress value between 0 and 100 (or 0..1 scale, depending on usage).</returns>
    private static double InterpolateProgress(
        double start,
        double end,
        TimeSpan elapsedTime,
        TimeSpan duration,
        EasingFunction easing)
    {
        // Clamp the progress fraction t between 0 and 1
        var t = Math.Clamp(elapsedTime.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
        var easedT = ApplyEasing(t, easing);
        return start + ((end - start) * easedT);
    }

    /// <summary>
    ///     Applies the specified <paramref name="easing" /> function to the normalized time value <paramref name="t" />.
    /// </summary>
    /// <param name="t">Normalized time (0 to 1).</param>
    /// <param name="easing">Which easing function to apply.</param>
    /// <returns>The eased time value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ApplyEasing(double t, EasingFunction easing)
    {
        const double tolerance = 0.0001;
        return easing switch
        {
            EasingFunction.Linear => t,
            EasingFunction.EaseInQuad => t * t,
            EasingFunction.EaseOutQuad => t * (2 - t),
            EasingFunction.EaseInOutCubic =>
                t < 0.5
                    ? 4 * t * t * t
                    : ((t - 1) * ((2 * t) - 2) * ((2 * t) - 2)) + 1,
            EasingFunction.EaseInExpo => t == 0 ? 0 : Math.Pow(2, 10 * (t - 1)),
            EasingFunction.EaseOutExpo => Math.Abs(t - 1) < tolerance ? 1 : 1 - Math.Pow(2, -10 * t),
            _ => t
        };
    }

    /// <summary>
    ///     Generates a sequence of progress values for smooth animation from <paramref name="start" />
    ///     to <paramref name="end" /> over the given <paramref name="duration" />, using the specified
    ///     <paramref name="easing" /> function and frames per second (<paramref name="fps" />).
    /// </summary>
    /// <param name="start">Initial progress value.</param>
    /// <param name="end">Target progress value.</param>
    /// <param name="duration">Total animation duration.</param>
    /// <param name="easing">Easing function to interpolate with.</param>
    /// <param name="fps">Frames (updates) per second.</param>
    /// <param name="cancellationToken">
    ///     A token to cancel iteration; throwing <see cref="OperationCanceledException" />.
    /// </param>
    /// <returns>An async stream of double values representing the interpolated progress.</returns>
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
