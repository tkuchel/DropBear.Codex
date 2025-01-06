#region

using System.Collections.Concurrent;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

/// <summary>
///     Represents a single step in the <see cref="DropBearProgressBar" /> component.
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
    ///     Required configuration defining step details (ID, Name, Tooltip, etc.).
    /// </summary>
    [Parameter]
    [EditorRequired]
    public required ProgressStepConfig Config { get; set; }

    /// <summary>
    ///     The raw progress value for this step (0-100).
    /// </summary>
    [Parameter]
    public double Progress { get; set; }

    /// <summary>
    ///     The current status of the step (e.g., NotStarted, InProgress, Completed, Failed).
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
    ///     The position of this step relative to others (Previous, Current, Next).
    /// </summary>
    [Parameter]
    public StepPosition Position { get; set; }

    /// <summary>
    ///     Indicates whether a transition animation is currently in progress.
    /// </summary>
    public bool IsTransitioning { get; private set; }

    /// <summary>
    ///     The display value (possibly smoothed) used for the progress bar width.
    /// </summary>
    public double DisplayProgress { get; private set; }

    /// <summary>
    ///     The duration this step has been in progress, if it has started.
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
            // If there's no existing transition and the displayed progress isn't the target,
            // queue a new transition.
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
                                       startProgress, targetProgress, duration,
                                       Config.EasingFunction, cancellationToken: token))
                    {
                        if (IsDisposed)
                        {
                            break;
                        }

                        nextTransition.UpdateProgress(progress);
                        DisplayProgress = progress;
                        await InvokeAsync(StateHasChanged);
                    }

                    nextTransition.Complete();
                }
                catch (OperationCanceledException)
                {
                    // Transition was canceled.
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
            // Cancel and dispose any active transition
            if (_currentTransition != null)
            {
                _currentTransition.Dispose();
                _currentTransition = null;
            }

            // Clear and dispose queued transitions
            while (_transitionQueue.TryDequeue(out var transition))
            {
                transition.Dispose();
            }

            _transitionLock.Dispose();
        }

        await base.DisposeAsync(disposing);
    }

    private string GetPositionClass()
    {
        return Position switch
        {
            StepPosition.Previous => "previous-step",
            StepPosition.Current => "current-step",
            StepPosition.Next => "next-step",
            _ => string.Empty
        };
    }

    private string GetStatusClass()
    {
        return Status switch
        {
            StepStatus.Completed => "success",
            StepStatus.InProgress => "current",
            StepStatus.Warning => "warning",
            StepStatus.Failed => "error",
            StepStatus.Skipped => "skipped",
            _ => string.Empty
        };
    }

    private string GetStatusText()
    {
        return Position switch
        {
            StepPosition.Previous => "Previous Step",
            StepPosition.Current => "Current Step",
            StepPosition.Next => "Next Step",
            _ => string.Empty
        };
    }
}

/// <summary>
///     The position of a step relative to the entire progress sequence.
/// </summary>
public enum StepPosition
{
    /// <summary>
    ///     Step is behind the current step.
    /// </summary>
    Previous,

    /// <summary>
    ///     Step is the current step in progress.
    /// </summary>
    Current,

    /// <summary>
    ///     Step is next (upcoming).
    /// </summary>
    Next
}
