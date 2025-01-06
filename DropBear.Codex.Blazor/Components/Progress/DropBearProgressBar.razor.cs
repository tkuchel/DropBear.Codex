#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

/// <summary>
///     A versatile progress bar component that supports indeterminate, normal (0-100), and stepped progress modes.
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
    ///     Indicates whether the progress bar is currently indeterminate.
    /// </summary>
    [Parameter]
    public bool IsIndeterminate { get; set; }

    /// <summary>
    ///     The message displayed above the progress bar (e.g., "Loading...").
    /// </summary>
    [Parameter]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     The overall numeric progress (0..100).
    /// </summary>
    [Parameter]
    public double Progress { get; set; }

    /// <summary>
    ///     A list of step configurations for stepped progress mode.
    /// </summary>
    [Parameter]
    public IReadOnlyList<ProgressStepConfig>? Steps { get; set; }

    /// <summary>
    ///     The minimum time (ms) to display each step before moving to the next.
    /// </summary>
    [Parameter]
    public int MinStepDisplayTimeMs { get; set; } = 500;

    /// <summary>
    ///     If true, progress transitions are eased smoothly instead of jumping.
    /// </summary>
    [Parameter]
    public bool UseSmoothProgress { get; set; } = true;

    /// <summary>
    ///     The easing function to apply to progress transitions (e.g., EaseInOutCubic).
    /// </summary>
    [Parameter]
    public EasingFunction EasingFunction { get; set; } = EasingFunction.EaseInOutCubic;

    /// <summary>
    ///     Event raised when a step's status changes (e.g., from InProgress to Completed).
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
                _currentSteps = [..Steps];
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
        // Only update if the component is initialized and not disposed.
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            var shouldUpdate = false;

            await _updateLock.WaitAsync();
            try
            {
                if (_state is null)
                {
                    return;
                }

                // Check if Indeterminate or message/progress changed significantly.
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
    ///     Public method to trigger a re-render from external code.
    /// </summary>
    private void RequestRender()
    {
        if (!IsDisposed)
        {
            try
            {
                InvokeAsync(StateHasChanged);
            }
            catch (ObjectDisposedException)
            {
                // Component already disposed, ignore
            }
        }
    }

    /// <summary>
    ///     Updates the progress and status of a particular step in stepped mode.
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

                // If the step is completed or failed/skipped, move to next step automatically
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

        // Cancel any smoothing in progress
        await _smoothingCts?.CancelAsync()!;
        _smoothingCts = new CancellationTokenSource();

        try
        {
            var token = _smoothingCts.Token;
            _currentStepIndex++;

            // Force minimum display time if smoothing is enabled
            if (UseSmoothProgress)
            {
                await Task.Delay(MinStepDisplayTimeMs, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Transition canceled
        }
    }

    /// <summary>
    ///     Returns a small subset of steps (previous, current, next) for display, if relevant.
    /// </summary>
    private IEnumerable<(ProgressStepConfig Config, int Index)> GetVisibleSteps()
    {
        if (_currentSteps == null)
        {
            yield break;
        }

        // Show up to 3 steps: the previous, current, and next.
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
                if (_smoothingCts != null)
                {
                    await _smoothingCts.CancelAsync();
                    _smoothingCts.Dispose();
                }

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

    #region Public Methods for External Control

    /// <summary>
    ///     Switches the progress bar into indeterminate mode (no numeric progress).
    /// </summary>
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
    ///     Sets the progress bar to a specific numeric value (0-100) and updates the message.
    /// </summary>
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
    ///     Updates the step configurations for stepped progress mode.
    /// </summary>
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

        RequestRender();
    }

    /// <summary>
    ///     Sets all parameters at once: mode (indeterminate or not), progress, message, and steps.
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

        RequestRender();
    }

    #endregion
}
