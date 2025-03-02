#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

/// <summary>
///     A versatile progress bar component that supports indeterminate, normal, and stepped progress modes.
///     Optimized for Blazor Server.
/// </summary>
public sealed partial class DropBearProgressBar : DropBearComponentBase
{
    #region UI Update Methods

    /// <summary>
    ///     Requests a UI render update if the component is not disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task RequestRenderAsync()
    {
        if (!IsDisposed)
        {
            try
            {
                _shouldRender = true;
                await InvokeAsync(StateHasChanged);
            }
            catch (ObjectDisposedException)
            {
                // Ignore if disposed
            }
        }
    }

    #endregion

    #region Disposal

    /// <summary>
    ///     Disposes the component and releases all resources.
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            await _disposalCts.CancelAsync();

            if (_smoothingCts != null)
            {
                await _smoothingCts.CancelAsync();
                _smoothingCts.Dispose();
            }

            if (_state != null)
            {
                await _state.DisposeAsync();
                _statePool.Return(_state);
                _state = null;
            }

            _updateLock.Dispose();
        }
        catch (Exception ex)
        {
            LogError("Error disposing progress bar", ex);
        }

        _disposalCts.Dispose();
        await base.DisposeAsyncCore();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Gets a human-readable string representing the estimated time remaining for the operation.
    /// </summary>
    /// <returns>A formatted time string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetEstimatedTimeRemaining()
    {
        if (_state is null || _state.OverallProgress <= 0)
        {
            return string.Empty;
        }

        var elapsed = DateTime.UtcNow - _state.StartTime;
        var estimatedTotal = TimeSpan.FromTicks((long)(elapsed.Ticks / (_state.OverallProgress / 100)));
        var remaining = estimatedTotal - elapsed;

        return $"Estimated time remaining: {FormatTimeSpan(remaining)}";
    }

    /// <summary>
    ///     Formats a TimeSpan into a human-readable string.
    /// </summary>
    /// <param name="span">The TimeSpan to format.</param>
    /// <returns>A formatted time string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatTimeSpan(TimeSpan span)
    {
        if (span.TotalHours >= 1)
        {
            return $"{span.TotalHours:F1} hours";
        }

        if (span.TotalMinutes >= 1)
        {
            return $"{span.TotalMinutes:F0} minutes";
        }

        return $"{span.TotalSeconds:F0} seconds";
    }

    #endregion

    #region Constants & Fields

    private const double MinProgress = 0;
    private const double MaxProgress = 100;
    private readonly CancellationTokenSource _disposalCts = new();

    private readonly ProgressStatePool _statePool = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    private int _currentStepIndex;
    private List<ProgressStepConfig>? _currentSteps;
    private volatile bool _isInitialized;
    private string? _lastMessage = string.Empty;
    private double _lastProgress;
    private CancellationTokenSource? _smoothingCts;
    private ProgressState? _state;

    // Backing fields for parameters
    private bool _isIndeterminate;
    private string _message = string.Empty;
    private double _progress;
    private IReadOnlyList<ProgressStepConfig>? _steps;
    private int _minStepDisplayTimeMs = 500;
    private bool _useSmoothProgress = true;
    private EasingFunction _easingFunction = EasingFunction.EaseInOutCubic;

    // Flag to track if component should render
    private bool _shouldRender = true;

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets whether the progress bar is in indeterminate mode.
    /// </summary>
    [Parameter]
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            if (_isIndeterminate != value)
            {
                _isIndeterminate = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the message displayed next to the progress bar.
    /// </summary>
    [Parameter]
    public string Message
    {
        get => _message;
        set
        {
            if (_message != value)
            {
                _message = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the current progress value (0-100).
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
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the steps to display in the progress bar.
    /// </summary>
    [Parameter]
    public IReadOnlyList<ProgressStepConfig>? Steps
    {
        get => _steps;
        set
        {
            if (_steps != value)
            {
                _steps = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the minimum time to display a step before moving to the next one (in milliseconds).
    /// </summary>
    [Parameter]
    public int MinStepDisplayTimeMs
    {
        get => _minStepDisplayTimeMs;
        set
        {
            if (_minStepDisplayTimeMs != value)
            {
                _minStepDisplayTimeMs = value;
            }
        }
    }

    /// <summary>
    ///     Gets or sets whether to use smooth progress transitions.
    /// </summary>
    [Parameter]
    public bool UseSmoothProgress
    {
        get => _useSmoothProgress;
        set
        {
            if (_useSmoothProgress != value)
            {
                _useSmoothProgress = value;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the easing function to use for progress transitions.
    /// </summary>
    [Parameter]
    public EasingFunction EasingFunction
    {
        get => _easingFunction;
        set
        {
            if (_easingFunction != value)
            {
                _easingFunction = value;
            }
        }
    }

    /// <summary>
    ///     Event callback that is invoked when a step's state changes.
    /// </summary>
    [Parameter]
    public EventCallback<(string StepId, StepStatus Status)> OnStepStateChanged { get; set; }

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

    /// <summary>
    ///     Initializes the component, setting up state management.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _state = _statePool.Get();
            _smoothingCts = new CancellationTokenSource();
            await InitializeStateAsync();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize progress bar", ex);
            throw;
        }
    }

    /// <summary>
    ///     Initializes the component state based on parameter values.
    /// </summary>
    private async Task InitializeStateAsync()
    {
        await _updateLock.WaitAsync();
        try
        {
            if (_isIndeterminate)
            {
                await _state!.SetIndeterminateAsync(_message);
            }
            else
            {
                await _state!.UpdateOverallProgressAsync(_progress, _message);
            }

            if (_steps?.Any() == true)
            {
                _currentSteps = [.._steps];
                foreach (var step in _currentSteps)
                {
                    _state.GetOrCreateStepState(step.Id);
                }
            }
        }
        finally
        {
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Updates the component when parameters change.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _updateLock.WaitAsync(_disposalCts.Token);
            try
            {
                if (_state is null)
                {
                    return;
                }

                var shouldUpdate =
                    _isIndeterminate != _state.IsIndeterminate ||
                    _message != _lastMessage ||
                    (!_isIndeterminate && Math.Abs(_progress - _lastProgress) > 0.001);

                if (shouldUpdate)
                {
                    _lastMessage = _message;
                    _lastProgress = _progress;

                    if (_isIndeterminate)
                    {
                        await _state.SetIndeterminateAsync(_message, _disposalCts.Token);
                    }
                    else
                    {
                        await _state.UpdateOverallProgressAsync(_progress, _message, _disposalCts.Token);
                    }

                    _shouldRender = true;
                }
            }
            finally
            {
                _updateLock.Release();
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            LogError("Failed to update parameters", ex);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Updates the progress and status of a specific step.
    /// </summary>
    /// <param name="stepId">The ID of the step to update.</param>
    /// <param name="progress">The new progress value (0-100).</param>
    /// <param name="status">The new status of the step.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task UpdateStepProgressAsync(
        string stepId,
        double progress,
        StepStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );

        try
        {
            await _updateLock.WaitAsync(cts.Token);
            try
            {
                var stepState = _state!.GetOrCreateStepState(stepId);
                var previousStatus = stepState.Status;

                await stepState.UpdateProgressAsync(progress, status, cts.Token);

                if (status != previousStatus)
                {
                    await OnStepStateChanged.InvokeAsync((stepId, status));
                }

                if (status is StepStatus.Completed or StepStatus.Failed or StepStatus.Skipped)
                {
                    await MoveToNextStepAsync();
                }

                await RequestRenderAsync();
            }
            finally
            {
                _updateLock.Release();
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            LogError("Failed to update step progress", ex);
        }
    }

    /// <summary>
    ///     Moves to the next step in the sequence.
    /// </summary>
    private async Task MoveToNextStepAsync()
    {
        if (_currentSteps == null || _currentStepIndex >= _currentSteps.Count - 1)
        {
            return;
        }

        await _smoothingCts?.CancelAsync()!;
        _smoothingCts?.Dispose();
        _smoothingCts = new CancellationTokenSource();

        try
        {
            _currentStepIndex++;

            if (_useSmoothProgress)
            {
                await Task.Delay(_minStepDisplayTimeMs, _smoothingCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Transition cancelled
        }
    }

    /// <summary>
    ///     Gets the steps that should be visible in the UI.
    /// </summary>
    /// <returns>A sequence of visible steps with their position indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IEnumerable<(ProgressStepConfig Config, int Index)> GetVisibleSteps()
    {
        if (_currentSteps == null)
        {
            yield break;
        }

        var startIdx = Math.Max(0, _currentStepIndex - 1);
        var endIdx = Math.Min(_currentSteps.Count - 1, startIdx + 2);

        for (var i = startIdx; i <= endIdx; i++)
        {
            yield return (_currentSteps[i], i - startIdx);
        }
    }

    /// <summary>
    ///     Sets the progress bar to indeterminate mode.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task SetIndeterminateModeAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );

        await _updateLock.WaitAsync(cts.Token);
        try
        {
            _isIndeterminate = true;
            _message = message;
            _progress = MinProgress;

            if (_isInitialized && _state != null)
            {
                await _state.SetIndeterminateAsync(message, cts.Token);
            }
        }
        finally
        {
            _updateLock.Release();
        }

        await RequestRenderAsync();
    }

    /// <summary>
    ///     Sets the progress bar to normal (determinate) mode with a specific progress value and message.
    /// </summary>
    /// <param name="progress">The progress value (0-100).</param>
    /// <param name="message">The message to display.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task SetNormalProgressAsync(
        double progress,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );

        await _updateLock.WaitAsync(cts.Token);
        try
        {
            _isIndeterminate = false;
            _progress = Math.Clamp(progress, MinProgress, MaxProgress);
            _message = message;

            if (_isInitialized && _state != null)
            {
                await _state.UpdateOverallProgressAsync(_progress, _message, cts.Token);
            }
        }
        finally
        {
            _updateLock.Release();
        }

        await RequestRenderAsync();
    }

    /// <summary>
    ///     Sets the steps to be displayed in the progress bar.
    /// </summary>
    /// <param name="steps">The steps configuration.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task SetStepsAsync(
        IReadOnlyList<ProgressStepConfig>? steps,
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );

        await _updateLock.WaitAsync(cts.Token);
        try
        {
            _steps = steps;

            if (_isInitialized && steps?.Any() == true)
            {
                _currentSteps = [..steps];
                foreach (var step in _currentSteps)
                {
                    _state?.GetOrCreateStepState(step.Id);
                }
            }
            else
            {
                _currentSteps = null;
            }
        }
        finally
        {
            _updateLock.Release();
        }

        await RequestRenderAsync();
    }

    /// <summary>
    ///     Sets all parameters of the progress bar at once.
    /// </summary>
    /// <param name="isIndeterminate">Whether the progress bar is in indeterminate mode.</param>
    /// <param name="progress">The progress value (0-100).</param>
    /// <param name="message">The message to display.</param>
    /// <param name="steps">The steps configuration.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task SetParametersManuallyAsync(
        bool isIndeterminate,
        double progress,
        string message,
        IReadOnlyList<ProgressStepConfig>? steps,
        CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _disposalCts.Token,
            cancellationToken
        );

        await _updateLock.WaitAsync(cts.Token);
        try
        {
            _isIndeterminate = isIndeterminate;
            _progress = Math.Clamp(progress, MinProgress, MaxProgress);
            _message = message;
            _steps = steps;

            if (_isInitialized && _state != null)
            {
                if (isIndeterminate)
                {
                    await _state.SetIndeterminateAsync(message, cts.Token);
                }
                else
                {
                    await _state.UpdateOverallProgressAsync(_progress, _message, cts.Token);
                }

                if (steps?.Any() == true)
                {
                    _currentSteps = [..steps];
                    foreach (var step in _currentSteps)
                    {
                        _state.GetOrCreateStepState(step.Id);
                    }
                }
                else
                {
                    _currentSteps = null;
                }
            }
        }
        finally
        {
            _updateLock.Release();
        }

        await RequestRenderAsync();
    }

    #endregion
}
