#region

using System.Collections.ObjectModel;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.ObjectPool;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

public sealed partial class DropBearProgressBar : DropBearComponentBase
{
    private const int MinimumProgressDuration = 300;
    private const int MinimumStepDuration = 500;
    private const double ProgressThreshold = 0.01;
    private const int MaxRetryAttempts = 3;
    private readonly HashSet<string> _failedSteps = new(StringComparer.Ordinal);

    private readonly CancellationTokenSource _progressCts = new();
    private readonly string _progressId = $"progress-{Guid.NewGuid():N}";

    private readonly ObjectPool<ProgressStep> _stepPool =
        new DefaultObjectPool<ProgressStep>(new DefaultPooledObjectPolicy<ProgressStep>());

    private readonly SemaphoreSlim _updateLock = new(1, 1);

    private int _focusedStepIndex;
    private ProgressStep? _hoveredStep;
    private bool _isDisposed;
    private bool _isIndeterminate;
    private bool _isInitialized;
    private bool _keyboardFocus;
    private DateTime _lastUpdateTime = DateTime.MinValue;
    private double _overallProgress;
    private ObservableCollection<ProgressStep> _steps = new();
    private double _taskProgress;

    [Parameter] public ProgressBarType Type { get; set; } = ProgressBarType.Standard;
    [Parameter] public double Progress { get; set; }
    [Parameter] public IEnumerable<ProgressStep>? Steps { get; set; }
    [Parameter] public string? Label { get; set; } = "Overall Progress";
    [Parameter] public EventCallback<double> ProgressChanged { get; set; }
    [Parameter] public EventCallback<ProgressStep> StepCompleted { get; set; }
    [Parameter] public EventCallback<ProgressStep> OnStepSelect { get; set; }
    [Parameter] public bool EnableSmoothTransitions { get; set; } = true;

    public double TaskProgress
    {
        get => _taskProgress;
        private set
        {
            if (Math.Abs(_taskProgress - value) > ProgressThreshold)
            {
                _taskProgress = Math.Clamp(value, 0, 100);
                InvokeAsync(StateHasChanged);
            }
        }
    }

    public double OverallProgress
    {
        get => _overallProgress;
        private set
        {
            if (Math.Abs(_overallProgress - value) > ProgressThreshold)
            {
                _overallProgress = Math.Clamp(value, 0, 100);
                InvokeAsync(StateHasChanged);
            }
        }
    }

    private string GetProgressClass()
    {
        return Type switch
        {
            ProgressBarType.Indeterminate => "indeterminate",
            ProgressBarType.Stepped => "with-steps",
            _ => "standard"
        };
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (Type == ProgressBarType.Stepped && Steps?.Any() == true)
        {
            _steps = new ObservableCollection<ProgressStep>(Steps);
        }

        _isIndeterminate = Type == ProgressBarType.Indeterminate;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeWithRetryAsync();
        }
    }

    private async Task InitializeWithRetryAsync()
    {
        var delay = TimeSpan.FromMilliseconds(100);
        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                await InitializeJsInterop();
                return;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "JS initialization attempt {Attempt} failed", attempt);
                if (attempt == MaxRetryAttempts)
                {
                    throw;
                }

                await Task.Delay(delay * attempt);
            }
        }
    }

    private async Task InitializeJsInterop()
    {
        if (_isDisposed)
        {
            return;
        }

        await SafeJsVoidInteropAsync(
            "DropBearProgressBar.initialize",
            _progressId,
            DotNetObjectReference.Create(this));
        _isInitialized = true;
    }

    protected override Task OnParametersSetAsync()
    {
        if (Steps != null && !Steps.SequenceEqual(_steps))
        {
            RecycleSteps();
            _steps = new ObservableCollection<ProgressStep>(Steps);
        }

        return base.OnParametersSetAsync();
    }

    public async Task UpdateProgressAsync(double taskProgress, int completedTasks, int totalTasks)
    {
        if (_isDisposed || _isIndeterminate)
        {
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_progressCts.Token);
        try
        {
            await _updateLock.WaitAsync(cts.Token);

            var timeSinceLastUpdate = DateTime.UtcNow - _lastUpdateTime;
            var shouldSmooth = EnableSmoothTransitions &&
                               timeSinceLastUpdate.TotalMilliseconds < MinimumStepDuration;

            var clampedTaskProgress = Math.Clamp(taskProgress, 0, 100);
            OverallProgress = totalTasks > 0
                ? Math.Clamp((double)completedTasks / totalTasks * 100, 0, 100)
                : 0;

            if (shouldSmooth && clampedTaskProgress - TaskProgress > 5)
            {
                await SimulateSmoothProgress(TaskProgress, clampedTaskProgress);
            }
            else
            {
                TaskProgress = clampedTaskProgress;
            }

            if (_isInitialized)
            {
                await ProgressChanged.InvokeAsync(OverallProgress);
                await UpdateStepDisplayAsync(GetCurrentStepIndex());
            }

            _lastUpdateTime = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Progress update cancelled");
        }
        catch (ObjectDisposedException)
        {
            Logger.Debug("Progress update skipped - component disposed");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating progress");
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public async Task MarkStepCompleteAsync(string stepName)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_progressCts.Token);
        try
        {
            await _updateLock.WaitAsync(cts.Token);
            await UpdateStepStatusAsync(stepName, StepStatus.Completed);
            var step = _steps.FirstOrDefault(s => s.Name == stepName);
            if (step != null)
            {
                await StepCompleted.InvokeAsync(step);
            }
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public async Task MarkStepFailedAsync(string stepName, string? errorMessage = null)
    {
        _failedSteps.Add(stepName);
        await UpdateStepStatusAsync(stepName, StepStatus.Error);
    }

    private async Task SimulateSmoothProgress(double start, double end, int duration = MinimumProgressDuration)
    {
        if (_isDisposed || start >= end || !_isInitialized)
        {
            return;
        }

        var startTime = DateTime.Now;
        while ((DateTime.Now - startTime).TotalMilliseconds < duration && !_progressCts.Token.IsCancellationRequested)
        {
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            var progress = Easing(elapsed / duration);
            TaskProgress = start + ((end - start) * progress);
            await InvokeAsync(StateHasChanged);
            await Task.Delay(16, _progressCts.Token);
        }

        TaskProgress = end;
        await InvokeAsync(StateHasChanged);
    }

    private static double Easing(double t)
    {
        return t < 0.5 ? 2 * t * t : -1 + ((4 - (2 * t)) * t);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!_keyboardFocus)
        {
            return;
        }

        switch (e.Key)
        {
            case "ArrowLeft" when _focusedStepIndex > 0:
                _focusedStepIndex--;
                break;
            case "ArrowRight" when _focusedStepIndex < _steps.Count - 1:
                _focusedStepIndex++;
                break;
            case "Enter":
                var step = _steps.ElementAt(_focusedStepIndex);
                await OnStepSelect.InvokeAsync(step);
                break;
        }
    }

    private void OnStepHover(ProgressStep step)
    {
        _hoveredStep = step;
        StateHasChanged();
    }

    private async Task UpdateStepStatusAsync(string stepName, StepStatus status)
    {
        if (_isDisposed)
        {
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_progressCts.Token);
        try
        {
            await _updateLock.WaitAsync(cts.Token);
            var step = _steps.FirstOrDefault(s => s.Name == stepName);
            if (step != null)
            {
                step.Status = status;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Step status update cancelled");
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private async Task UpdateStepDisplayAsync(int currentIndex)
    {
        if (!_isInitialized || _isDisposed)
        {
            return;
        }

        try
        {
            await SafeJsVoidInteropAsync(
                "DropBearProgressBar.updateStepDisplay",
                _progressId,
                currentIndex,
                _steps.Count);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating step display");
        }
    }

    private int GetCurrentStepIndex()
    {
        var activeStep = _steps.FirstOrDefault(s => s.Status == StepStatus.Active);
        return activeStep != null
            ? _steps.ToList().IndexOf(activeStep)
            : _steps.Count(s => s.Status == StepStatus.Completed);
    }

    private void RecycleSteps()
    {
        foreach (var step in _steps)
        {
            _stepPool.Return(step);
        }

        _steps.Clear();
    }

    [JSInvokable]
    public async Task HandleJsError(string error)
    {
        Logger.Error("JS error occurred: {Error}", error);
        if (_isInitialized)
        {
            await InitializeJsInterop();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            await _progressCts.CancelAsync();
            RecycleSteps();

            if (_isInitialized)
            {
                await SafeJsVoidInteropAsync("DropBearProgressBar.dispose", _progressId);
            }
        }
        catch (JSDisconnectedException)
        {
            Logger.Warning("JSInterop for DropBearProgressBar already disposed");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing progress bar");
        }
        finally
        {
            _isDisposed = true;
            _progressCts.Dispose();
            _updateLock.Dispose();
            await base.DisposeAsync();
        }
    }
}
