#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Provides efficient progress interpolation and easing functions for progress bars.
///     Optimized for memory usage and CPU performance in Blazor Server.
/// </summary>
internal static class ProgressInterpolation
{
    private const double Precision = 0.001;
    private const double TimingPrecision = 0.0001;
    private const int MaxIterations = 1000;
    private const int DefaultFps = 60;
    private static readonly TimeSpan MinFrameTime = TimeSpan.FromMilliseconds(1000.0 / DefaultFps);

    /// <summary>
    ///     Calculates a smoothed progress value using optimized interpolation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double InterpolateProgress(
        double start,
        double end,
        TimeSpan elapsedTime,
        TimeSpan duration,
        EasingFunction easing)
    {
        var t = Math.Clamp(elapsedTime.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
        var easedT = ApplyEasing(t, easing);
        return Math.FusedMultiplyAdd(end - start, easedT, start); // Uses FMA for better precision
    }

    /// <summary>
    ///     Applies easing functions with optimized calculations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ApplyEasing(double t, EasingFunction easing)
    {
        return easing switch
        {
            EasingFunction.Linear => t,
            EasingFunction.EaseInQuad => t * t,
            EasingFunction.EaseOutQuad => t * (2 - t),
            EasingFunction.EaseInOutCubic => EaseInOutCubicOptimized(t),
            EasingFunction.EaseInExpo => t == 0 ? 0 : Math.Pow(2, 10 * (t - 1)),
            EasingFunction.EaseOutExpo => IsApproximatelyOne(t) ? 1 : 1 - Math.Pow(2, -10 * t),
            _ => t
        };
    }

    /// <summary>
    ///     Optimized implementation of cubic easing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double EaseInOutCubicOptimized(double t)
    {
        if (t < 0.5)
        {
            return 4 * t * t * t;
        }

        var u = (2 * t) - 2;
        return (0.5 * u * u * u) + 1;
    }

    /// <summary>
    ///     Fast approximate equality check for timing values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsApproximatelyOne(double value)
    {
        return Math.Abs(value - 1) < TimingPrecision;
    }

    /// <summary>
    ///     Generates an optimized sequence of progress values for smooth animation.
    /// </summary>
    public static async IAsyncEnumerable<double> GenerateProgressSequence(
        double start,
        double end,
        TimeSpan duration,
        EasingFunction easing,
        int fps = DefaultFps,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Input validation
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        if (fps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fps));
        }

        // Ensure reasonable frame time
        var frameTime = TimeSpan.FromSeconds(1.0 / Math.Min(fps, 120));
        if (frameTime < MinFrameTime)
        {
            frameTime = MinFrameTime;
        }

        using var sequence = new ProgressSequenceGenerator(
            start, end, duration, easing, frameTime, cancellationToken);

        await foreach (var value in sequence.GenerateSequence(cancellationToken).ConfigureAwait(false))
        {
            yield return value;
        }
    }

    /// <summary>
    ///     Helper class to manage progress sequence state.
    /// </summary>
    private sealed class ProgressSequenceGenerator : IDisposable
    {
        private readonly CancellationToken _cancellationToken;
        private readonly TimeSpan _duration;
        private readonly EasingFunction _easing;
        private readonly double _end;
        private readonly TimeSpan _frameTime;
        private readonly double _start;
        private readonly PeriodicTimer _timer;
        private bool _isDisposed;

        public ProgressSequenceGenerator(
            double start,
            double end,
            TimeSpan duration,
            EasingFunction easing,
            TimeSpan frameTime,
            CancellationToken cancellationToken)
        {
            _start = start;
            _end = end;
            _duration = duration;
            _easing = easing;
            _frameTime = frameTime;
            _cancellationToken = cancellationToken;
            _timer = new PeriodicTimer(frameTime);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _timer.Dispose();
        }

        public async IAsyncEnumerable<double> GenerateSequence(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var elapsedTime = TimeSpan.Zero;
            var iterations = 0;
            var lastValue = _start;

            while (!_isDisposed &&
                   elapsedTime < _duration &&
                   iterations++ < MaxIterations)
            {
                using var linkedCts = CancellationTokenSource
                    .CreateLinkedTokenSource(_cancellationToken, cancellationToken);

                try
                {
                    if (!await _timer.WaitForNextTickAsync(linkedCts.Token))
                    {
                        yield break;
                    }
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }

                var currentValue = InterpolateProgress(
                    _start, _end, elapsedTime, _duration, _easing);

                if (Math.Abs(currentValue - lastValue) >= Precision)
                {
                    yield return currentValue;
                    lastValue = currentValue;
                }

                elapsedTime += _frameTime;
            }

            if (!_isDisposed && Math.Abs(lastValue - _end) >= Precision)
            {
                yield return _end;
            }
        }
    }
}
