#region

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

    [Parameter] public bool IsIndeterminate { get; set; }
    [Parameter] public string Message { get; set; } = string.Empty;
    [Parameter] public double Progress { get; set; }
    [Parameter] public IReadOnlyList<ProgressStepConfig>? Steps { get; set; }
    [Parameter] public int MinStepDisplayTimeMs { get; set; } = 500;
    [Parameter] public bool UseSmoothProgress { get; set; } = true;
    [Parameter] public EasingFunction EasingFunction { get; set; } = EasingFunction.EaseInOutCubic;
    [Parameter] public EventCallback<(string StepId, StepStatus Status)> OnStepStateChanged { get; set; }

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
                    IsIndeterminate != _state.IsIndeterminate ||
                    Message != _lastMessage ||
                    (!IsIndeterminate && Math.Abs(Progress - _lastProgress) > 0.001);

                if (shouldUpdate)
                {
                    _lastMessage = Message;
                    _lastProgress = Progress;

                    if (IsIndeterminate)
                    {
                        await _state.SetIndeterminateAsync(Message, _disposalCts.Token);
                    }
                    else
                    {
                        await _state.UpdateOverallProgressAsync(Progress, Message, _disposalCts.Token);
                    }

                    await InvokeAsync(StateHasChanged);
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

    private async Task RequestRenderAsync()
    {
        if (!IsDisposed)
        {
            try
            {
                await InvokeAsync(StateHasChanged);
            }
            catch (ObjectDisposedException)
            {
                // Ignore if disposed
            }
        }
    }

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
                    await MoveToNextStepAsync(cts.Token);
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

    private async Task MoveToNextStepAsync(CancellationToken cancellationToken)
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

            if (UseSmoothProgress)
            {
                await Task.Delay(MinStepDisplayTimeMs, _smoothingCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Transition cancelled
        }
    }

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
            IsIndeterminate = true;
            Message = message;
            Progress = MinProgress;

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
            IsIndeterminate = false;
            Progress = Math.Clamp(progress, MinProgress, MaxProgress);
            Message = message;

            if (_isInitialized && _state != null)
            {
                await _state.UpdateOverallProgressAsync(Progress, Message, cts.Token);
            }
        }
        finally
        {
            _updateLock.Release();
        }

        await RequestRenderAsync();
    }

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

        await RequestRenderAsync();
    }

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
            IsIndeterminate = isIndeterminate;
            Progress = Math.Clamp(progress, MinProgress, MaxProgress);
            Message = message;
            Steps = steps;

            if (_isInitialized && _state != null)
            {
                if (isIndeterminate)
                {
                    await _state.SetIndeterminateAsync(message, cts.Token);
                }
                else
                {
                    await _state.UpdateOverallProgressAsync(Progress, Message, cts.Token);
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

    public override async ValueTask DisposeAsync()
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
        await base.DisposeAsync();
    }
}
