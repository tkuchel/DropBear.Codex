#region

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

/// <summary>
///     Modern progress bar component optimized for .NET 9+ and Blazor Server.
///     Features smooth animations, accessibility support, and responsive design.
/// </summary>
public sealed partial class DropBearProgressBar : DropBearComponentBase
{
    #region Constants & Static Members

    private const double MinProgress = 0;
    private const double MaxProgress = 100;
    private static readonly TimeSpan DefaultAnimationDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan EstimationUpdateInterval = TimeSpan.FromSeconds(1);

    #endregion

    #region Fields

    private readonly CancellationTokenSource _componentCts = new();
    private readonly Dictionary<string, ProgressStepState> _stepStates = new(StringComparer.OrdinalIgnoreCase);

    private DateTime _startTime = DateTime.UtcNow;
    private DateTime _lastUpdateTime = DateTime.UtcNow;
    private double _lastProgress;
    private bool _hasStarted;

    // Cached computations for performance
    private string? _cachedProgressStyle;
    private double _cachedProgressValue = -1;
    private string? _cachedTimeEstimation;
    private DateTime _lastTimeEstimationUpdate = DateTime.MinValue;

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets whether the progress bar is in indeterminate mode.
    /// </summary>
    [Parameter]
    public bool IsIndeterminate { get; set; }

    /// <summary>
    ///     Gets or sets the main message displayed above the progress bar.
    /// </summary>
    [Parameter]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the current progress value (0-100).
    /// </summary>
    [Parameter]
    public double Progress { get; set; }

    /// <summary>
    ///     Gets or sets the collection of progress steps.
    ///     When set directly (not through parameter binding), this property
    ///     automatically initializes step states for progress tracking.
    /// </summary>
    [Parameter]
    public IReadOnlyList<ProgressStepConfig>? Steps
    {
        get => _steps;
        set
        {
            if (ReferenceEquals(_steps, value)) return;

            _steps = value;

            // Initialize step states when steps are set directly
            // This is necessary because direct property assignment bypasses OnParametersSetAsync
            if (_steps is { Count: > 0 })
            {
                foreach (var step in _steps)
                {
                    if (!_stepStates.ContainsKey(step.Id))
                    {
                        _stepStates[step.Id] = new ProgressStepState(step.Id);
                    }
                }
            }
        }
    }
    private IReadOnlyList<ProgressStepConfig>? _steps;

    /// <summary>
    ///     Gets or sets the visual variant/theme of the progress bar.
    /// </summary>
    [Parameter]
    public ProgressVariant Variant { get; set; } = ProgressVariant.Primary;

    /// <summary>
    ///     Gets or sets the layout mode for progress steps.
    /// </summary>
    [Parameter]
    public StepsLayout StepsLayout { get; set; } = StepsLayout.Horizontal;

    /// <summary>
    ///     Gets or sets whether to show percentage text.
    /// </summary>
    [Parameter]
    public bool ShowPercentage { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to show estimated time remaining.
    /// </summary>
    [Parameter]
    public bool ShowEstimatedTime { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to show a subtle glow effect on the progress fill.
    /// </summary>
    [Parameter]
    public bool ShowProgressGlow { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to use compact mode for steps display.
    /// </summary>
    [Parameter]
    public bool CompactSteps { get; set; }

    /// <summary>
    ///     Gets or sets whether to show status icons.
    /// </summary>
    [Parameter]
    public bool ShowStatusIcon { get; set; } = true;

    /// <summary>
    ///     Gets or sets a custom status message.
    /// </summary>
    [Parameter]
    public string? StatusMessage { get; set; }

    /// <summary>
    ///     Gets or sets the animation duration for progress changes.
    /// </summary>
    [Parameter]
    public TimeSpan AnimationDuration { get; set; } = DefaultAnimationDuration;

    /// <summary>
    ///     Event callback fired when progress changes.
    /// </summary>
    [Parameter]
    public EventCallback<double> OnProgressChanged { get; set; }

    /// <summary>
    ///     Event callback fired when a step changes status.
    /// </summary>
    [Parameter]
    public EventCallback<(string StepId, StepStatus Status)> OnStepStatusChanged { get; set; }

    /// <summary>
    ///     Event callback fired when progress is completed.
    /// </summary>
    [Parameter]
    public EventCallback OnCompleted { get; set; }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the current effective progress value.
    /// </summary>
    public double CurrentProgress => IsIndeterminate ? 0 : Math.Clamp(Progress, MinProgress, MaxProgress);

    /// <summary>
    ///     Gets whether this progress bar has steps configured.
    /// </summary>
    public bool HasSteps => Steps?.Count > 0;

    /// <summary>
    ///     Gets whether the progress operation has started.
    /// </summary>
    public bool IsStarted => _hasStarted || CurrentProgress > 0;

    /// <summary>
    ///     Gets whether the progress is completed.
    /// </summary>
    public bool IsCompleted => !IsIndeterminate && CurrentProgress >= MaxProgress;

    /// <summary>
    ///     Gets the total elapsed time since progress started.
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.UtcNow - _startTime;

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Component initialization.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _startTime = DateTime.UtcNow;
        _lastUpdateTime = _startTime;

        // Initialize step states
        if (HasSteps)
        {
            foreach (var step in Steps!)
            {
                _stepStates[step.Id] = new ProgressStepState(step.Id);
            }
        }
    }

    /// <summary>
    ///     Handles parameter updates with optimized change detection.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        var currentTime = DateTime.UtcNow;
        var progressChanged = Math.Abs(Progress - _lastProgress) > 0.01;

        // Reinitialize step states when Steps parameter changes
        // This handles the case where Steps is set after component initialization
        if (HasSteps)
        {
            var needsReinit = _stepStates.Count == 0 ||
                              Steps!.Any(s => !_stepStates.ContainsKey(s.Id));

            if (needsReinit)
            {
                foreach (var step in Steps!)
                {
                    if (!_stepStates.ContainsKey(step.Id))
                    {
                        _stepStates[step.Id] = new ProgressStepState(step.Id);
                    }
                }
            }
        }

        if (progressChanged)
        {
            if (!_hasStarted && Progress > 0)
            {
                _hasStarted = true;
                _startTime = currentTime;
            }

            // Clear cached values when progress changes
            _cachedProgressStyle = null;
            _cachedProgressValue = -1;

            _lastProgress = Progress;
            _lastUpdateTime = currentTime;

            await OnProgressChanged.InvokeAsync(CurrentProgress);

            // Check for completion
            if (IsCompleted && OnCompleted.HasDelegate)
            {
                await OnCompleted.InvokeAsync();
            }
        }

        await base.OnParametersSetAsync();
    }

    /// <summary>
    ///     Optimized rendering control.
    /// </summary>
    protected override bool ShouldRender()
    {
        // Only render if we have meaningful changes
        return !IsDisposed && (
            Math.Abs(Progress - _lastProgress) > 0.01 ||
            _cachedProgressStyle == null ||
            (ShowEstimatedTime && ShouldUpdateTimeEstimation())
        );
    }

    /// <summary>
    ///     Component disposal.
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        await _componentCts.CancelAsync();
        _componentCts.Dispose();

        await base.DisposeAsyncCore();
    }

    #endregion

    #region Public API Methods

    /// <summary>
    ///     Updates progress with optional smooth animation.
    /// </summary>
    /// <param name="newProgress">The new progress value (0-100).</param>
    /// <param name="message">Optional message update.</param>
    /// <param name="animateTransition">Whether to animate the transition.</param>
    public async Task UpdateProgressAsync(double newProgress, string? message = null, bool animateTransition = true)
    {
        if (IsDisposed) return;

        Progress = Math.Clamp(newProgress, MinProgress, MaxProgress);

        if (!string.IsNullOrEmpty(message))
        {
            Message = message;
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    ///     Updates the status of a specific step.
    /// </summary>
    /// <param name="stepId">The ID of the step to update.</param>
    /// <param name="progress">The step's progress (0-100).</param>
    /// <param name="status">The step's status.</param>
    public async Task UpdateStepAsync(string stepId, double progress, StepStatus status)
    {
        if (IsDisposed || !_stepStates.TryGetValue(stepId, out var stepState))
            return;

        var oldStatus = stepState.Status;
        stepState.UpdateProgress(progress, status);

        if (status != oldStatus)
        {
            await OnStepStatusChanged.InvokeAsync((stepId, status));
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    ///     Resets the progress bar to initial state.
    /// </summary>
    public async Task ResetAsync()
    {
        if (IsDisposed) return;

        Progress = MinProgress;
        _hasStarted = false;
        _startTime = DateTime.UtcNow;
        _lastUpdateTime = _startTime;
        _cachedProgressStyle = null;
        _cachedProgressValue = -1;
        _cachedTimeEstimation = null;

        foreach (var stepState in _stepStates.Values)
        {
            stepState.Reset();
        }

        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Gets the CSS style for the progress fill element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetProgressFillStyle()
    {
        if (IsIndeterminate)
        {
            return string.Empty;
        }

        var progress = CurrentProgress;
        if (Math.Abs(progress - _cachedProgressValue) < 0.1 && _cachedProgressStyle != null)
        {
            return _cachedProgressStyle;
        }

        _cachedProgressValue = progress;
        _cachedProgressStyle = $"width: {progress:F1}%; transition-duration: {AnimationDuration.TotalMilliseconds}ms";

        return _cachedProgressStyle;
    }

    /// <summary>
    ///     Gets formatted estimated time remaining.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetEstimatedTimeRemaining()
    {
        if (!ShowEstimatedTime || IsIndeterminate || CurrentProgress <= 0 || CurrentProgress >= 100)
        {
            return string.Empty;
        }

        var now = DateTime.UtcNow;
        if (now - _lastTimeEstimationUpdate < EstimationUpdateInterval && _cachedTimeEstimation != null)
        {
            return _cachedTimeEstimation;
        }

        var elapsed = now - _startTime;
        var rate = CurrentProgress / elapsed.TotalSeconds;
        var remainingProgress = 100 - CurrentProgress;
        var estimatedSeconds = remainingProgress / rate;
        var estimatedRemaining = TimeSpan.FromSeconds(estimatedSeconds);

        _cachedTimeEstimation = FormatTimeSpan(estimatedRemaining);
        _lastTimeEstimationUpdate = now;

        return _cachedTimeEstimation;
    }

    /// <summary>
    ///     Gets the effective layout based on step count when Auto is selected.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private StepsLayout GetEffectiveLayout()
    {
        if (StepsLayout != StepsLayout.Auto || !HasSteps)
        {
            return StepsLayout;
        }

        var stepCount = Steps!.Count;
        return stepCount switch
        {
            <= 4 => StepsLayout.Horizontal,
            <= 6 => StepsLayout.Compact,
            <= 10 => StepsLayout.Timeline,
            _ => StepsLayout.Dense
        };
    }

    /// <summary>
    ///     Gets whether we have many steps (6+) for applying special styling.
    /// </summary>
    private bool HasManySteps => HasSteps && Steps!.Count >= 6;

    /// <summary>
    ///     Gets the step count category for CSS data attribute.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetStepCountCategory()
    {
        if (!HasSteps) return "none";
        var count = Steps!.Count;
        return count switch
        {
            <= 4 => "few",
            <= 8 => "many",
            _ => "very-many"
        };
    }

    /// <summary>
    ///     Formats a TimeSpan for display.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatTimeSpan(TimeSpan span)
    {
        // Handle negative timespans
        if (span < TimeSpan.Zero)
        {
            return "0s";
        }

        // For spans >= 1 hour, show hours and minutes
        if (span.TotalHours >= 1)
        {
            // Use total hours (rounded down) for spans > 24 hours
            var totalHours = (int)Math.Floor(span.TotalHours);
            return $"{totalHours}h {span.Minutes:D2}m";
        }

        // For spans >= 1 minute, show minutes and seconds
        if (span.TotalMinutes >= 1)
        {
            return $"{span.Minutes}m {span.Seconds:D2}s";
        }

        // For spans < 1 minute, show seconds with proper rounding
        var totalSeconds = span.TotalSeconds;

        // Use Math.Round with AwayFromZero to ensure 0.5 rounds up to 1
        var roundedSeconds = (int)Math.Round(totalSeconds, MidpointRounding.AwayFromZero);

        return $"{roundedSeconds}s";
    }

    /// <summary>
    ///     Determines if time estimation should be updated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldUpdateTimeEstimation()
    {
        return ShowEstimatedTime &&
               DateTime.UtcNow - _lastTimeEstimationUpdate >= EstimationUpdateInterval;
    }

    /// <summary>
    ///     Gets the visible steps with their positions.
    /// </summary>
    private IEnumerable<(ProgressStepConfig Config, StepPosition Position)> GetVisibleSteps()
    {
        if (!HasSteps) yield break;

        var steps = Steps!;
        var totalSteps = steps.Count;
        var currentIndex = (int)(Progress / 100.0 * totalSteps);
        currentIndex = Math.Min(currentIndex, totalSteps - 1);

        for (var i = 0; i < totalSteps; i++)
        {
            var position = i switch
            {
                _ when i < currentIndex => StepPosition.Previous,
                _ when i == currentIndex => StepPosition.Current,
                _ => StepPosition.Next
            };

            yield return (steps[i], position);
        }
    }

    /// <summary>
    ///     Gets the progress for a specific step.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double GetStepProgress(string stepId)
    {
        return _stepStates.TryGetValue(stepId, out var state) ? state.Progress : 0;
    }

    /// <summary>
    ///     Gets the status for a specific step.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private StepStatus GetStepStatus(string stepId)
    {
        return _stepStates.TryGetValue(stepId, out var state) ? state.Status : StepStatus.NotStarted;
    }

    /// <summary>
    ///     Gets the current overall status type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetCurrentStatusType()
    {
        if (IsCompleted) return "success";
        if (HasSteps && _stepStates.Values.Any(s => s.Status == StepStatus.Failed)) return "error";
        if (HasSteps && _stepStates.Values.Any(s => s.Status == StepStatus.Warning)) return "warning";
        if (IsStarted) return "info";
        return "default";
    }

    /// <summary>
    ///     Gets the appropriate status icon markup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MarkupString GetStatusIcon()
    {
        var iconSvg = GetCurrentStatusType() switch
        {
            "success" =>
                """<svg viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clip-rule="evenodd"/></svg>""",
            "error" =>
                """<svg viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/></svg>""",
            "warning" =>
                """<svg viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/></svg>""",
            "info" =>
                """<svg viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>""",
            _ =>
                """<svg viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clip-rule="evenodd"/></svg>"""
        };

        return new MarkupString(iconSvg);
    }

    #endregion
}

