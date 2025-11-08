#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

/// <summary>
///     Optimized step component for DropBearProgressBar with modern .NET 9+ features.
/// </summary>
public sealed partial class DropBearProgressBarStep : DropBearComponentBase
{
    #region Constants

    private static readonly TimeSpan DefaultAnimationDuration = TimeSpan.FromMilliseconds(250);

    #endregion

    #region Fields

    private DateTime _statusChangedTime = DateTime.UtcNow;
    private StepStatus _previousStatus = StepStatus.NotStarted;
    private double _previousProgress;

    // Cached values for performance
    private string? _cachedProgressStyle;
    private double _cachedProgressValue = -1;
    private string? _cachedPositionClass;
    private StepPosition _cachedPosition = (StepPosition)(-1);
    private string? _cachedStatusAttribute;
    private StepStatus _cachedStatus = (StepStatus)(-1);

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets the step configuration.
    /// </summary>
    [Parameter, EditorRequired]
    public required ProgressStepConfig Config { get; set; }

    /// <summary>
    ///     Gets or sets the current progress (0-100).
    /// </summary>
    [Parameter]
    public double Progress { get; set; }

    /// <summary>
    ///     Gets or sets the current status of the step.
    /// </summary>
    [Parameter]
    public StepStatus Status { get; set; } = StepStatus.NotStarted;

    /// <summary>
    ///     Gets or sets the position of this step relative to the current step.
    /// </summary>
    [Parameter]
    public StepPosition Position { get; set; }

    /// <summary>
    ///     Gets or sets the visual variant.
    /// </summary>
    [Parameter]
    public ProgressVariant Variant { get; set; } = ProgressVariant.Primary;

    /// <summary>
    ///     Gets or sets whether to display in compact mode.
    /// </summary>
    [Parameter]
    public bool CompactMode { get; set; }

    /// <summary>
    ///     Gets or sets whether to show the progress bar within the step.
    /// </summary>
    [Parameter]
    public bool ShowProgressBar { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to show pulse animation for in-progress steps.
    /// </summary>
    [Parameter]
    public bool ShowPulseAnimation { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to show duration information.
    /// </summary>
    [Parameter]
    public bool ShowDuration { get; set; } = true;

    /// <summary>
    ///     Gets or sets the animation duration for transitions.
    /// </summary>
    [Parameter]
    public TimeSpan AnimationDuration { get; set; } = DefaultAnimationDuration;

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the time elapsed since the status last changed.
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.UtcNow - _statusChangedTime;

    /// <summary>
    ///     Gets whether this step is currently active.
    /// </summary>
    public bool IsActive => Position == StepPosition.Current;

    /// <summary>
    ///     Gets whether this step has completed.
    /// </summary>
    public bool IsCompleted => Status == StepStatus.Completed;

    /// <summary>
    ///     Gets whether this step has failed.
    /// </summary>
    public bool HasFailed => Status == StepStatus.Failed;

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Handles parameter changes with optimized caching.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        // Track status changes for timing
        if (Status != _previousStatus)
        {
            _statusChangedTime = DateTime.UtcNow;
            _previousStatus = Status;
            _cachedStatusAttribute = null; // Clear cache
        }

        // Clear cached styles when progress changes significantly
        if (Math.Abs(Progress - _previousProgress) > 0.1)
        {
            _cachedProgressStyle = null;
            _cachedProgressValue = -1;
            _previousProgress = Progress;
        }

        await base.OnParametersSetAsync();
    }

    /// <summary>
    ///     Optimized render control.
    /// </summary>
    protected override bool ShouldRender()
    {
        return !IsDisposed && (
            Status != _previousStatus ||
            Math.Abs(Progress - _previousProgress) > 0.1 ||
            Position != _cachedPosition ||
            _cachedProgressStyle == null
        );
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Gets the CSS class for the step position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetPositionClass()
    {
        if (Position == _cachedPosition && _cachedPositionClass != null)
        {
            return _cachedPositionClass;
        }

        _cachedPosition = Position;
        _cachedPositionClass = Position switch
        {
            StepPosition.Previous => "step-previous",
            StepPosition.Current => "step-current",
            StepPosition.Next => "step-next",
            _ => string.Empty
        };

        return _cachedPositionClass;
    }

    /// <summary>
    ///     Gets the CSS class for compact mode.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetCompactClass()
    {
        return CompactMode ? "step-compact" : string.Empty;
    }

    /// <summary>
    ///     Gets the data-status attribute value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetStatusAttribute()
    {
        if (Status == _cachedStatus && _cachedStatusAttribute != null)
        {
            return _cachedStatusAttribute;
        }

        _cachedStatus = Status;
        _cachedStatusAttribute = Status switch
        {
            StepStatus.NotStarted => "not-started",
            StepStatus.InProgress => "in-progress",
            StepStatus.Completed => "completed",
            StepStatus.Warning => "warning",
            StepStatus.Failed => "failed",
            StepStatus.Skipped => "skipped",
            _ => "unknown"
        };

        return _cachedStatusAttribute;
    }

    /// <summary>
    ///     Gets the progress bar style with caching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetProgressStyle()
    {
        var progress = Math.Clamp(Progress, 0, 100);

        if (Math.Abs(progress - _cachedProgressValue) < 0.1 && _cachedProgressStyle != null)
        {
            return _cachedProgressStyle;
        }

        _cachedProgressValue = progress;
        _cachedProgressStyle = $"width: {progress:F1}%; transition-duration: {AnimationDuration.TotalMilliseconds}ms;";

        return _cachedProgressStyle;
    }

    /// <summary>
    ///     Gets the appropriate status icon markup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MarkupString GetStatusIcon()
    {
        var iconSvg = Status switch
        {
            StepStatus.NotStarted => """<svg viewBox="0 0 20 20" fill="currentColor"><circle cx="10" cy="10" r="3"/></svg>""",
            StepStatus.InProgress => """<svg class="animate-spin" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm0-2a6 6 0 100-12 6 6 0 000 12z" clip-rule="evenodd" opacity="0.25"/><path d="M10 2a8 8 0 018 8h-2a6 6 0 00-6-6V2z"/></svg>""",
            StepStatus.Completed => """<svg viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clip-rule="evenodd"/></svg>""",
            StepStatus.Warning => """<svg viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/></svg>""",
            StepStatus.Failed => """<svg viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/></svg>""",
            StepStatus.Skipped => """<svg viewBox="0 0 20 20" fill="currentColor"><path d="M4.555 5.168A1 1 0 003 6v8a1 1 0 001.555.832L10 11.202V14a1 1 0 001.555.832l6-4a1 1 0 000-1.664l-6-4A1 1 0 0010 6v2.798l-5.445-3.63z"/></svg>""",
            _ => """<svg viewBox="0 0 20 20" fill="currentColor"><circle cx="10" cy="10" r="2"/></svg>"""
        };

        return new MarkupString(iconSvg);
    }

    /// <summary>
    ///     Gets the status message for the current step state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetStatusMessage()
    {
        return Status switch
        {
            StepStatus.NotStarted => "Waiting to start",
            StepStatus.InProgress => "In progress...",
            StepStatus.Completed => "Completed successfully",
            StepStatus.Warning => "Completed with warnings",
            StepStatus.Failed => "Failed",
            StepStatus.Skipped => "Skipped",
            _ => string.Empty
        };
    }

    /// <summary>
    ///     Formats the duration for display.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string FormatDuration()
    {
        var elapsed = ElapsedTime;

        // Handle negative timespans
        if (elapsed < TimeSpan.Zero)
        {
            return "0s";
        }

        // For durations >= 1 hour, show hours and minutes
        if (elapsed.TotalHours >= 1)
        {
            // Use total hours (rounded down) for spans > 24 hours
            var totalHours = (int)Math.Floor(elapsed.TotalHours);
            return $"{totalHours}h {elapsed.Minutes:D2}m";
        }

        // For durations >= 1 minute, show minutes and seconds
        if (elapsed.TotalMinutes >= 1)
        {
            return $"{elapsed.Minutes}m {elapsed.Seconds:D2}s";
        }

        // For durations < 1 minute, show seconds with proper rounding
        var totalSeconds = elapsed.TotalSeconds;

        // Use Math.Round with AwayFromZero to ensure 0.5 rounds up to 1
        var roundedSeconds = (int)Math.Round(totalSeconds, MidpointRounding.AwayFromZero);

        return $"{roundedSeconds}s";
    }

    #endregion
}
