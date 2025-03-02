#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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
    #region Transition Management

    /// <summary>
    ///     Starts the next pending progress transition if one is available.
    /// </summary>
    private async Task StartNextTransitionAsync()
    {
        await _transitionLock.WaitAsync();
        try
        {
            if (_isTransitioning || _transitionQueue.IsEmpty)
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
            var startProgress = _displayProgress;
            var targetProgress = _progress;
            var duration = TimeSpan.FromMilliseconds(_config?.MinimumDisplayTimeMs ?? 500);

            // Start the transition in a separate task.
            _ = Task.Run(async () =>
            {
                try
                {
                    // Generate a sequence of interpolated progress values.
                    await foreach (var progress in ProgressInterpolation.GenerateProgressSequence(
                                       startProgress, targetProgress, duration,
                                       _config?.EasingFunction ?? EasingFunction.EaseInOutCubic,
                                       cancellationToken: token))
                    {
                        if (IsDisposed)
                        {
                            break;
                        }

                        // Update the transition and display progress.
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
                    // Ensure state updates are synchronized.
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

                    // Recursively start the next transition if queued.
                    await StartNextTransitionAsync();
                }
            }, token);
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    #endregion

    #region Disposal

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        // Cancel and dispose the active transition if any.
        if (_currentTransition != null)
        {
            _currentTransition.Dispose();
            _currentTransition = null;
        }

        // Dispose any queued transitions.
        while (_transitionQueue.TryDequeue(out var transition))
        {
            transition.Dispose();
        }

        _transitionLock.Dispose();

        await base.DisposeAsyncCore();
    }

    #endregion

    #region Fields & Constants

    // Lock used to synchronize transition animations.
    private readonly SemaphoreSlim _transitionLock = new(1, 1);

    // Queue to hold pending progress transitions.
    private readonly ConcurrentQueue<ProgressTransition> _transitionQueue = new();

    // The currently active transition (if any).
    private ProgressTransition? _currentTransition;

    // Tracking variables for step progress and status.
    private bool _hasStarted;
    private DateTimeOffset _startTime = DateTimeOffset.MinValue;
    private StepStatus _status = StepStatus.NotStarted;

    // Backing fields for parameters
    private ProgressStepConfig? _config;
    private double _progress;
    private StepPosition _position;

    // Internal state
    private double _displayProgress;
    private bool _isTransitioning;

    // Flag to track if component should render
    private bool _shouldRender = true;

    // Cached class names
    private string? _cachedPositionClass;
    private string? _cachedStatusClass;

    #endregion

    #region Properties & Parameters

    /// <summary>
    ///     Required configuration defining step details (ID, Name, Tooltip, etc.).
    /// </summary>
    [Parameter]
    [EditorRequired]
    public required ProgressStepConfig Config
    {
        get => _config!;
        set
        {
            if (_config != value)
            {
                _config = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     The raw progress value for this step (0-100).
    /// </summary>
    [Parameter]
    public double Progress
    {
        get => _progress;
        set
        {
            if (Math.Abs(_progress - value) > 0.001)
            {
                _progress = value;
                // Don't set _shouldRender here because we'll update through transitions
            }
        }
    }

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
            _cachedStatusClass = null; // Reset cached class
            _shouldRender = true;

            // Record start time when the step transitions into InProgress.
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
    public StepPosition Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                _position = value;
                _cachedPositionClass = null; // Reset cached class
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Indicates whether a transition animation is currently in progress.
    /// </summary>
    public bool IsTransitioning
    {
        get => _isTransitioning;
        private set
        {
            if (_isTransitioning != value)
            {
                _isTransitioning = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     The display value (possibly smoothed) used for the progress bar width.
    /// </summary>
    public double DisplayProgress
    {
        get => _displayProgress;
        private set
        {
            if (Math.Abs(_displayProgress - value) > 0.001)
            {
                _displayProgress = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     The duration this step has been in progress, if it has started.
    /// </summary>
    public TimeSpan RunningTime => _hasStarted ? DateTimeOffset.UtcNow - _startTime : TimeSpan.Zero;

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Controls whether the component should render, optimizing for performance.
    /// </summary>
    /// <returns>True if the component should render, false otherwise.</returns>
    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            // If no transition is active and the displayed progress is not at the target,
            // queue a new transition.
            if (_transitionQueue.IsEmpty && !_isTransitioning && Math.Abs(_displayProgress - _progress) > 0.001)
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

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Returns the CSS class corresponding to the step's position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetPositionClass()
    {
        if (_cachedPositionClass != null)
        {
            return _cachedPositionClass;
        }

        _cachedPositionClass = _position switch
        {
            StepPosition.Previous => "previous-step",
            StepPosition.Current => "current-step",
            StepPosition.Next => "next-step",
            _ => string.Empty
        };

        return _cachedPositionClass;
    }

    /// <summary>
    ///     Returns the CSS class corresponding to the step's status.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetStatusClass()
    {
        if (_cachedStatusClass != null)
        {
            return _cachedStatusClass;
        }

        _cachedStatusClass = _status switch
        {
            StepStatus.Completed => "success",
            StepStatus.InProgress => "current",
            StepStatus.Warning => "warning",
            StepStatus.Failed => "error",
            StepStatus.Skipped => "skipped",
            _ => string.Empty
        };

        return _cachedStatusClass;
    }

    /// <summary>
    ///     Returns a textual description of the step's position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetStatusText()
    {
        return _position switch
        {
            StepPosition.Previous => "Previous Step",
            StepPosition.Current => "Current Step",
            StepPosition.Next => "Next Step",
            _ => string.Empty
        };
    }

    #endregion
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
