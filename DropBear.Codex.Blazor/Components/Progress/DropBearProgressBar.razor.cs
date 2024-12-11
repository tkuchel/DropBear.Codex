#region

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.ObjectPool;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

/// <summary>
///     A customizable progress bar component that can operate in different modes: Standard, Indeterminate, or Stepped.
///     Supports smooth transitions, step-based progress, and keyboard navigation.
/// </summary>
public sealed partial class DropBearProgressBar : DropBearComponentBase
{
    private const int MinimumProgressDuration = 300;
    private const int MinimumStepDuration = 500;
    private const double ProgressThreshold = 0.01;
    private const int MaxRetryAttempts = 3;

    // Tracks steps that have failed, to prevent repeated error states.
    private readonly HashSet<string> _failedSteps = new(StringComparer.Ordinal);

    // Token source for managing component lifecycle and cancelling async operations.
    private readonly CancellationTokenSource _progressCts = new();

    // Unique HTML element identifier.
    private readonly string _progressId = $"progress-{Guid.NewGuid():N}";

    // Object pool for reusing step objects, improving resource usage.
    private readonly ObjectPool<ProgressStep> _stepPool =
        new DefaultObjectPool<ProgressStep>(new DefaultPooledObjectPolicy<ProgressStep>());

    // Semaphore to ensure thread-safe updates to progress and steps.
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    // Internal state fields
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

    /// <summary>
    ///     Gets or sets the type of the progress bar (Standard, Indeterminate, Stepped).
    /// </summary>
    [Parameter]
    public ProgressBarType Type { get; set; } = ProgressBarType.Standard;

    /// <summary>
    ///     Defines the initial overall progress if needed (Primarily for Standard or Indeterminate bars).
    /// </summary>
    [Parameter]
    public double Progress { get; set; }

    /// <summary>
    ///     The collection of steps for stepped progress mode.
    /// </summary>
    [Parameter]
    public IEnumerable<ProgressStep>? Steps { get; set; }

    /// <summary>
    ///     Optional label describing the overall process.
    /// </summary>
    [Parameter]
    public string? Label { get; set; } = "Overall Progress";

    /// <summary>
    ///     Event callback invoked whenever the overall progress changes.
    /// </summary>
    [Parameter]
    public EventCallback<double> ProgressChanged { get; set; }

    /// <summary>
    ///     Event callback invoked when a step is completed.
    /// </summary>
    [Parameter]
    public EventCallback<ProgressStep> StepCompleted { get; set; }

    /// <summary>
    ///     Event callback invoked when a step is selected (e.g., via keyboard navigation or clicking).
    /// </summary>
    [Parameter]
    public EventCallback<ProgressStep> OnStepSelect { get; set; }

    /// <summary>
    ///     If true, enables smooth transitions of progress increments.
    /// </summary>
    [Parameter]
    public bool EnableSmoothTransitions { get; set; } = true;

    /// <summary>
    ///     The current task-level progress percentage.
    /// </summary>
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

    /// <summary>
    ///     The overall progress percentage.
    /// </summary>
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

    /// <summary>
    ///     Retrieves the appropriate CSS class based on the selected progress bar type.
    /// </summary>
    private string GetProgressClass()
    {
        return Type switch
        {
            ProgressBarType.Indeterminate => "indeterminate",
            ProgressBarType.Stepped => "with-steps",
            _ => "standard"
        };
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (Type == ProgressBarType.Stepped && Steps?.Any() == true)
        {
            _steps = new ObservableCollection<ProgressStep>(Steps);
        }

        _isIndeterminate = Type == ProgressBarType.Indeterminate;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeWithRetryAsync();
        }
    }

    /// <summary>
    ///     Attempts to initialize JS interop with retries in case of transient errors.
    /// </summary>
    private async Task InitializeWithRetryAsync()
    {
        var delay = TimeSpan.FromMilliseconds(100);
        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            if (_isDisposed)
            {
                return;
            }

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

    /// <summary>
    ///     Initializes JS interop by invoking the JS-side initialization method.
    /// </summary>
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

    /// <inheritdoc />
    protected override Task OnParametersSetAsync()
    {
        if (Steps != null && !Steps.SequenceEqual(_steps))
        {
            Logger.Debug("OnParametersSetAsync: Steps changed. OldCount={OldCount}, NewCount={NewCount}", _steps.Count,
                Steps.Count());
            RecycleSteps();
            _steps = new ObservableCollection<ProgressStep>(Steps);
            Logger.Debug("OnParametersSetAsync: Steps updated. StepsCount={StepsCount}", _steps.Count);
        }
        else
        {
            Logger.Debug("OnParametersSetAsync: No step changes detected or Steps is null. StepsCount={StepsCount}",
                _steps.Count);
        }

        return base.OnParametersSetAsync();
    }


    /// <summary>
    ///     Updates the progress bar with the current task progress and overall completion.
    /// </summary>
    /// <param name="taskProgress">Percentage of the current task progress (0-100).</param>
    /// <param name="completedTasks">Number of completed tasks.</param>
    /// <param name="totalTasks">Total number of tasks.</param>
    /// <returns></returns>
    public async Task UpdateProgressAsync(double taskProgress, int completedTasks, int totalTasks)
    {
        Logger.Debug(
            "UpdateProgressAsync called. TaskProgress={TaskProgress}, CompletedTasks={CompletedTasks}, TotalTasks={TotalTasks}",
            taskProgress, completedTasks, totalTasks);

        if (_isDisposed || _isIndeterminate)
        {
            Logger.Debug("UpdateProgressAsync: Skipped because disposed={Disposed} or indeterminate={Indeterminate}",
                _isDisposed, _isIndeterminate);
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_progressCts.Token);
        try
        {
            await _updateLock.WaitAsync(cts.Token);
            Logger.Debug("UpdateProgressAsync: Acquired update lock.");

            var timeSinceLastUpdate = DateTime.UtcNow - _lastUpdateTime;
            var shouldSmooth = EnableSmoothTransitions && timeSinceLastUpdate.TotalMilliseconds < MinimumStepDuration;

            var clampedTaskProgress = Math.Clamp(taskProgress, 0, 100);
            var newOverallProgress = totalTasks > 0
                ? Math.Clamp((double)completedTasks / totalTasks * 100, 0, 100)
                : 0;

            Logger.Debug("UpdateProgressAsync: Computed OverallProgress={OverallProgress}, ShouldSmooth={ShouldSmooth}",
                newOverallProgress, shouldSmooth);

            if (shouldSmooth && clampedTaskProgress - TaskProgress > 5)
            {
                Logger.Debug("UpdateProgressAsync: Starting smooth progress from {Start} to {End}", TaskProgress,
                    clampedTaskProgress);
                await SimulateSmoothProgress(TaskProgress, clampedTaskProgress);
            }
            else
            {
                Logger.Debug("UpdateProgressAsync: Setting TaskProgress directly to {Clamped}", clampedTaskProgress);
                await InvokeAsync(() => TaskProgress = clampedTaskProgress);
            }

            if (_isInitialized)
            {
                Logger.Debug("UpdateProgressAsync: Updating OverallProgress and invoking ProgressChanged");
                await InvokeAsync(async () =>
                {
                    OverallProgress = newOverallProgress;
                    await ProgressChanged.InvokeAsync(OverallProgress);
                    await UpdateStepDisplayAsync(GetCurrentStepIndex());
                });
            }

            _lastUpdateTime = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("UpdateProgressAsync: Operation canceled");
        }
        catch (ObjectDisposedException)
        {
            Logger.Debug("UpdateProgressAsync: Component disposed");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating progress");
        }
        finally
        {
            Logger.Debug("UpdateProgressAsync: Releasing update lock.");
            _updateLock.Release();
        }
    }


    /// <summary>
    ///     Marks a specific step as completed and triggers the StepCompleted event.
    /// </summary>
    /// <param name="stepName">Name of the step to complete.</param>
    public async Task MarkStepCompleteAsync(string stepName)
    {
        Logger.Debug("MarkStepCompleteAsync called for StepName={StepName}", stepName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_progressCts.Token);
        try
        {
            await _updateLock.WaitAsync(cts.Token);
            Logger.Debug("MarkStepCompleteAsync: Acquired update lock.");
            await UpdateStepStatusAsync(stepName, StepStatus.Completed);
            var step = _steps.FirstOrDefault(s => s.Name == stepName);
            if (step != null)
            {
                Logger.Debug("MarkStepCompleteAsync: Invoking StepCompleted for StepName={StepName}", stepName);
                await StepCompleted.InvokeAsync(step);
            }
        }
        finally
        {
            Logger.Debug("MarkStepCompleteAsync: Releasing update lock.");
            _updateLock.Release();
        }
    }

    /// <summary>
    ///     Marks a step as failed (in error state).
    /// </summary>
    /// <param name="stepName">Name of the failing step.</param>
    /// <param name="errorMessage">Optional error message.</param>
    public async Task MarkStepFailedAsync(string stepName, string? errorMessage = null)
    {
        Logger.Debug("MarkStepFailedAsync called for StepName={StepName}, ErrorMessage={ErrorMessage}", stepName,
            errorMessage);
        _failedSteps.Add(stepName);
        await UpdateStepStatusAsync(stepName, StepStatus.Error);
    }

    /// <summary>
    ///     Simulates smooth progress increment to create a visually appealing progress animation.
    /// </summary>
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

    /// <summary>
    ///     A simple easing function for smooth progress animation.
    /// </summary>
    private static double Easing(double t)
    {
        return t < 0.5 ? 2 * t * t : -1 + ((4 - (2 * t)) * t);
    }

    /// <summary>
    ///     Handles keyboard navigation between steps.
    /// </summary>
    /// <param name="e">Keyboard event arguments.</param>
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

    /// <summary>
    ///     Sets the hovered step for UI highlighting.
    /// </summary>
    /// <param name="step">The hovered step.</param>
    private void OnStepHover(ProgressStep step)
    {
        Logger.Debug("OnStepHover: HoveredStep={StepName}", step?.Name ?? "None");
        _hoveredStep = step;
        StateHasChanged();
    }

    /// <summary>
    ///     Updates the status of a given step (e.g., to Completed or Error).
    /// </summary>
    private async Task UpdateStepStatusAsync(string stepName, StepStatus status)
    {
        Logger.Debug("UpdateStepStatusAsync: StepName={StepName}, Status={Status}", stepName, status);
        if (_isDisposed)
        {
            Logger.Debug("UpdateStepStatusAsync: Skipped because component disposed.");
            return;
        }

        try
        {
            var step = _steps.FirstOrDefault(s => s.Name == stepName);
            if (step != null)
            {
                step.Status = status;
                await InvokeAsync(StateHasChanged);
                Logger.Debug("UpdateStepStatusAsync: Updated StepName={StepName} to Status={Status}", stepName, status);
            }
            else
            {
                Logger.Debug("UpdateStepStatusAsync: StepName={StepName} not found in _steps.", stepName);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("UpdateStepStatusAsync: Operation canceled");
        }
    }

    /// <summary>
    ///     Updates the step display via JS interop to reflect the current step visually.
    /// </summary>
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

    /// <summary>
    ///     Determines the currently active step index.
    /// </summary>
    private int GetCurrentStepIndex()
    {
        var activeStep = _steps.FirstOrDefault(s => s.Status == StepStatus.Active);
        var completedCount = _steps.Count(s => s.Status == StepStatus.Completed);
        var index = activeStep != null
            ? _steps.IndexOf(activeStep)
            : completedCount;

        // Add logging to understand what's happening
        Logger.Debug(
            "GetCurrentStepIndex called. StepsCount={StepsCount}, CompletedCount={CompletedCount}, ActiveStepName={ActiveStepName}, ReturningIndex={Index}",
            _steps.Count,
            completedCount,
            activeStep?.Name ?? "None",
            index);

        return index;
    }


    /// <summary>
    ///     Recycles step objects to the object pool for memory efficiency.
    /// </summary>
    private void RecycleSteps()
    {
        Logger.Debug("RecycleSteps: Returning {Count} steps to the pool.", _steps.Count);
        foreach (var step in _steps)
        {
            _stepPool.Return(step);
        }

        _steps.Clear();
        Logger.Debug("RecycleSteps: Steps cleared. StepsCount now={StepsCount}", _steps.Count);
    }

    /// <summary>
    ///     Handles JS-side errors by attempting re-initialization if needed.
    /// </summary>
    [JSInvokable]
    [ExcludeFromCodeCoverage]
    public async Task HandleJsError(string error)
    {
        Logger.Error("JS error occurred: {Error}", error);
        if (_isInitialized)
        {
            await InitializeJsInterop();
        }
    }

    /// <inheritdoc />
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
