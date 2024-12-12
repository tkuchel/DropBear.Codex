#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

/// <summary>
///     Represents a single step in the progress bar component
/// </summary>
public sealed partial class DropBearProgressBarStep : DropBearComponentBase
{
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private readonly ConcurrentQueue<ProgressTransition> _transitionQueue = new();
    private ProgressTransition? _currentTransition;
    private bool _hasStarted;
    private DateTimeOffset _startTime = DateTimeOffset.MinValue;
    private StepStatus _status = StepStatus.NotStarted;

    /// <summary>
    ///     Gets or sets the configuration for this step
    /// </summary>
    [Parameter]
    [EditorRequired]
    public required ProgressStepConfig Config { get; set; }

    /// <summary>
    ///     Gets or sets the current progress value (0-100)
    /// </summary>
    [Parameter]
    public double Progress { get; set; }

    /// <summary>
    ///     Gets or sets the current status of the step
    /// </summary>
    [Parameter]
    public StepStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;

            if (value == StepStatus.InProgress && !_hasStarted)
            {
                _hasStarted = true;
                _startTime = DateTimeOffset.UtcNow;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the step position (Previous, Current, or Next)
    /// </summary>
    [Parameter]
    public StepPosition Position { get; set; }

    /// <summary>
    ///     Gets whether this step is currently transitioning
    /// </summary>
    public bool IsTransitioning { get; private set; }

    /// <summary>
    ///     Gets the current display progress (smoothed)
    /// </summary>
    public double DisplayProgress { get; private set; }

    /// <summary>
    ///     Gets how long this step has been running
    /// </summary>
    public TimeSpan RunningTime => _hasStarted ? DateTimeOffset.UtcNow - _startTime : TimeSpan.Zero;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            // Don't start new transitions if we're disposing
            if (_transitionQueue.IsEmpty && !IsTransitioning && Math.Abs(DisplayProgress - Progress) > 0.001)
            {
                var transition = new ProgressTransition();
                _transitionQueue.Enqueue(transition);
                await StartNextTransitionAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in {ComponentName} OnParametersSetAsync", nameof(DropBearProgressBarStep));
        }
    }

    private async Task StartNextTransitionAsync()
    {
        await _transitionLock.WaitAsync();
        try
        {
            if (IsTransitioning || _transitionQueue.IsEmpty)
            {
                return;
            }

            if (!_transitionQueue.TryDequeue(out var nextTransition))
            {
                return;
            }

            _currentTransition = nextTransition;
            IsTransitioning = true;

            var token = nextTransition.CancellationToken;
            var startProgress = DisplayProgress;
            var targetProgress = Progress;
            var duration = TimeSpan.FromMilliseconds(Config.MinimumDisplayTimeMs);

            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var progress in ProgressInterpolation.GenerateProgressSequence(
                                       startProgress, targetProgress, duration, Config.EasingFunction,
                                       cancellationToken: token))
                    {
                        if (IsDisposed)
                        {
                            break;
                        }

                        nextTransition.UpdateProgress(progress);

                        // Added to help see why DisplayProgress isn't updating.
                        Logger.Debug("Step {StepId} progress: {Progress}", Config.Id, progress);

                        DisplayProgress = progress;

                        Logger.Debug("Step {StepId} DisplayProgress: {DisplayProgress}", Config.Id, DisplayProgress);

                        await InvokeAsync(StateHasChanged);
                    }

                    nextTransition.Complete();
                }
                catch (OperationCanceledException)
                {
                    // Transition was cancelled
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error during progress transition in {ComponentName}",
                        nameof(DropBearProgressBarStep));
                    nextTransition.Complete(false);
                }
                finally
                {
                    await _transitionLock.WaitAsync(token);
                    try
                    {
                        IsTransitioning = false;
                        if (_currentTransition == nextTransition)
                        {
                            _currentTransition = null;
                        }
                    }
                    finally
                    {
                        _transitionLock.Release();
                    }

                    // Start next transition if any
                    await StartNextTransitionAsync();
                }
            }, token);
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            // Cancel any ongoing transitions
            if (_currentTransition != null)
            {
                _currentTransition.Dispose();
                _currentTransition = null;
            }

            // Clear and dispose any queued transitions
            while (_transitionQueue.TryDequeue(out var transition))
            {
                transition.Dispose();
            }

            _transitionLock.Dispose();
        }

        await base.DisposeAsync(disposing);
    }
}

/// <summary>
///     Represents the position of a step in the progress window
/// </summary>
public enum StepPosition
{
    /// <summary>
    ///     Step is in the previous position
    /// </summary>
    Previous,

    /// <summary>
    ///     Step is in the current position
    /// </summary>
    Current,

    /// <summary>
    ///     Step is in the next position
    /// </summary>
    Next
}
