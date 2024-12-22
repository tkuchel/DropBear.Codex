#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

/// <summary>
///     A versatile progress bar component that supports indeterminate, normal, and stepped progress modes.
/// </summary>
public sealed partial class DropBearProgressBar : DropBearComponentBase
{
    private readonly ProgressStatePool _statePool = new();
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private int _currentStepIndex;
    private List<ProgressStepConfig>? _currentSteps;

    private bool _isInitialized;
    private string? _lastMessage = string.Empty;
    private double _lastProgress;
    private CancellationTokenSource? _smoothingCts;
    private ProgressState? _state;

    /// <summary>
    ///     Gets or sets whether the progress bar is in indeterminate mode.
    ///     <para>
    ///         Normally set by the parent component, but if you are driving this from
    ///         an external service, call <see cref="SetIndeterminateModeAsync" /> or
    ///         <see cref="SetNormalProgressAsync" /> to avoid warnings.
    ///     </para>
    /// </summary>
    [Parameter]
    public bool IsIndeterminate { get; set; }

    /// <summary>
    ///     Gets or sets the current message to display.
    /// </summary>
    [Parameter]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the overall progress (0-100).
    /// </summary>
    [Parameter]
    public double Progress { get; set; }

    /// <summary>
    ///     Gets or sets the step configurations when in stepped mode.
    /// </summary>
    [Parameter]
    public IReadOnlyList<ProgressStepConfig>? Steps { get; set; }

    /// <summary>
    ///     Gets or sets the minimum time to display each step in milliseconds.
    /// </summary>
    [Parameter]
    public int MinStepDisplayTimeMs { get; set; } = 500;

    /// <summary>
    ///     Gets or sets whether to use smooth progress transitions.
    /// </summary>
    [Parameter]
    public bool UseSmoothProgress { get; set; } = true;

    /// <summary>
    ///     Gets or sets the easing function for progress transitions.
    /// </summary>
    [Parameter]
    public EasingFunction EasingFunction { get; set; } = EasingFunction.EaseInOutCubic;

    /// <summary>
    ///     Event raised when a step's state changes.
    /// </summary>
    [Parameter]
    public EventCallback<(string StepId, StepStatus Status)> OnStepStateChanged { get; set; }

    /// <inheritdoc />
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
            Logger.Error(ex, "Error initializing {ComponentName}", nameof(DropBearProgressBar));
            throw;
        }
    }

    private async Task InitializeStateAsync()
    {
        await _updateLock.WaitAsync();
        try
        {
            if (IsIndeterminate)
            {
                await _state!.SetIndeterminateAsync(Message);
            }
            else
            {
                await _state!.UpdateOverallProgressAsync(Progress, Message);
            }

            if (Steps?.Any() == true)
            {
                _currentSteps = new List<ProgressStepConfig>(Steps);
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

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // If you are controlling this component from a parent that uses normal Blazor data binding,
        // this logic updates the internal state. If you're using the new public methods below,
        // the OnParametersSetAsync logic might be secondary or unused.
        if (!_isInitialized)
        {
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        try
        {
            var shouldUpdate = false;

            await _updateLock.WaitAsync();
            try
            {
                if (_state == null)
                {
                    return;
                }

                shouldUpdate =
                    IsIndeterminate != _state.IsIndeterminate ||
                    Message != _lastMessage ||
                    (!IsIndeterminate && Math.Abs(Progress - _lastProgress) > 0.001);

                if (shouldUpdate)
                {
                    _lastMessage = Message;
                    _lastProgress = Progress;

                    if (IsIndeterminate)
                    {
                        await _state.SetIndeterminateAsync(Message);
                    }
                    else
                    {
                        await _state.UpdateOverallProgressAsync(Progress, Message);
                    }
                }
            }
            finally
            {
                _updateLock.Release();
            }

            if (shouldUpdate)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            Logger.Error(ex, "Error in {ComponentName} OnParametersSetAsync", nameof(DropBearProgressBar));
        }
    }

    /// <summary>
    ///     Public method to request UI re-render from external consumers or services.
    /// </summary>
    public void RequestRender()
    {
        try
        {
            if (!IsDisposed)
            {
                InvokeAsync(StateHasChanged);
            }
        }
        catch (ObjectDisposedException)
        {
            // If the component is already disposed, ignore
        }
    }

    // ----------------------------------------------------------------------------------
    // PUBLIC METHODS that set the 'parameters' without triggering Blazor warnings
    // ----------------------------------------------------------------------------------

    /// <summary>
    ///     Sets the progress bar into indeterminate mode (e.g. unknown length).
    ///     Use this if you're driving the component from a service or orchestrator.
    /// </summary>
    /// <param name="message">A message to display (e.g. "Please wait...").</param>
    public async Task SetIndeterminateModeAsync(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync();
        try
        {
            IsIndeterminate = true;
            Message = message;
            Progress = 0;

            if (_isInitialized && _state != null)
            {
                await _state.SetIndeterminateAsync(message);
            }
        }
        finally
        {
            _updateLock.Release();
        }

        RequestRender();
    }

    /// <summary>
    ///     Sets the progress bar to normal (non-indeterminate) mode with a progress percentage.
    /// </summary>
    /// <param name="progress">A value from 0 to 100 representing overall progress.</param>
    /// <param name="message">The message to display.</param>
    public async Task SetNormalProgressAsync(double progress, string message)
    {
        if (IsDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync();
        try
        {
            IsIndeterminate = false;
            Progress = Math.Clamp(progress, 0, 100);
            Message = message;

            if (_isInitialized && _state != null)
            {
                await _state.UpdateOverallProgressAsync(Progress, Message);
            }
        }
        finally
        {
            _updateLock.Release();
        }

        RequestRender();
    }

    /// <summary>
    ///     Updates or sets the step configurations (stepped mode).
    /// </summary>
    /// <param name="steps">A list of step configurations, or null if none.</param>
    public async Task SetStepsAsync(IReadOnlyList<ProgressStepConfig>? steps)
    {
        if (IsDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync();
        try
        {
            Steps = steps;

            if (_isInitialized && steps?.Any() == true)
            {
                _currentSteps = new List<ProgressStepConfig>(steps);
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

        RequestRender();
    }

    /// <summary>
    ///     Updates both normal/indeterminate mode and steps in one go if desired.
    /// </summary>
    public async Task SetParametersManuallyAsync(
        bool isIndeterminate,
        double progress,
        string message,
        IReadOnlyList<ProgressStepConfig>? steps)
    {
        if (IsDisposed)
        {
            return;
        }

        await _updateLock.WaitAsync();
        try
        {
            IsIndeterminate = isIndeterminate;
            Progress = Math.Clamp(progress, 0, 100);
            Message = message;
            Steps = steps;

            if (_isInitialized && _state != null)
            {
                if (isIndeterminate)
                {
                    await _state.SetIndeterminateAsync(message);
                }
                else
                {
                    await _state.UpdateOverallProgressAsync(Progress, Message);
                }

                if (steps?.Any() == true)
                {
                    _currentSteps = new List<ProgressStepConfig>(steps);
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

        RequestRender();
    }
    // ----------------------------------------------------------------------------------

    /// <summary>
    ///     Updates the progress of a specific step in stepped mode.
    /// </summary>
    public async Task UpdateStepProgressAsync(string stepId, double progress, StepStatus status)
    {
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _updateLock.WaitAsync();
            try
            {
                var stepState = _state!.GetOrCreateStepState(stepId);
                var previousStatus = stepState.Status;

                await stepState.UpdateProgressAsync(progress, status);

                if (status != previousStatus)
                {
                    await OnStepStateChanged.InvokeAsync((stepId, status));
                }

                // Move to next step if completed
                if (status is StepStatus.Completed or StepStatus.Failed or StepStatus.Skipped)
                {
                    await MoveToNextStepAsync();
                }
            }
            finally
            {
                _updateLock.Release();
            }

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex) when (ex is not ObjectDisposedException)
        {
            Logger.Error(ex, "Error updating step progress in {ComponentName}", nameof(DropBearProgressBar));
        }
    }

    private async Task MoveToNextStepAsync()
    {
        if (_currentSteps == null || _currentStepIndex >= _currentSteps.Count - 1)
        {
            return;
        }

        // Cancel any ongoing smoothing
        await _smoothingCts?.CancelAsync();
        _smoothingCts = new CancellationTokenSource();

        try
        {
            var token = _smoothingCts.Token;
            _currentStepIndex++;

            // Ensure smooth transition even for fast steps
            if (UseSmoothProgress)
            {
                await Task.Delay(MinStepDisplayTimeMs, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Transition was cancelled
        }
    }

    private IEnumerable<(ProgressStepConfig Config, int Index)> GetVisibleSteps()
    {
        if (_currentSteps == null)
        {
            yield break;
        }

        // Always show 3 steps (previous, current, next)
        var startIdx = Math.Max(0, _currentStepIndex - 1);
        var endIdx = Math.Min(_currentSteps.Count - 1, startIdx + 2);

        for (var i = startIdx; i <= endIdx; i++)
        {
            yield return (_currentSteps[i], i - startIdx);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            try
            {
                await _smoothingCts?.CancelAsync();
                _smoothingCts?.Dispose();
                _updateLock.Dispose();

                if (_state != null)
                {
                    await _state.DisposeAsync();
                    _statePool.Return(_state);
                    _state = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error disposing {ComponentName}", nameof(DropBearProgressBar));
            }
        }

        await base.DisposeAsync(disposing);
    }
}
