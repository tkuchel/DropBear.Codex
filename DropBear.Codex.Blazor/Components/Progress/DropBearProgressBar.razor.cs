#region

using System.Collections.ObjectModel;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

public sealed partial class DropBearProgressBar : DropBearComponentBase
{
    private readonly string _progressId;
    private readonly SemaphoreSlim? _updateLock = new(1, 1);
    private ProgressStep? _hoveredStep;
    private bool _isDisposed;
    private bool _isIndeterminate;
    private bool _isInitialized;
    private double _overallProgress;
    private ObservableCollection<ProgressStep> _steps = new();
    private double _taskProgress;

    public DropBearProgressBar()
    {
        _progressId = $"progress-{Guid.NewGuid():N}";
    }

    [Parameter] public ProgressBarType Type { get; set; } = ProgressBarType.Standard;
    [Parameter] public double Progress { get; set; }
    [Parameter] public IEnumerable<ProgressStep>? Steps { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public EventCallback<double> ProgressChanged { get; set; }
    [Parameter] public EventCallback<ProgressStep> StepCompleted { get; set; }

    [Parameter]
    public double TaskProgress
    {
        get => _taskProgress;
        set
        {
            if (Math.Abs(_taskProgress - value) > 0.01) // Use small epsilon for floating point comparison
            {
                _taskProgress = value;
                InvokeAsync(StateHasChanged);
            }
        }
    }

    [Parameter]
    public double OverallProgress
    {
        get => _overallProgress;
        set
        {
            if (Math.Abs(_overallProgress - value) > 0.01) // Use small epsilon for floating point comparison
            {
                _overallProgress = value;
                InvokeAsync(StateHasChanged);
            }
        }
    }

    private void OnStepHover(ProgressStep step)
    {
        _hoveredStep = step;
        StateHasChanged();
    }

    private string GetStepIcon(ProgressStep step)
    {
        return step.Type switch
        {
            StepType.Users => "icon-users",
            StepType.Files => "icon-file-text",
            StepType.Database => "icon-database",
            _ => "icon-circle"
        };
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

    private string GetStepClass(ProgressStep step)
    {
        var classes = new List<string>();

        // Add status-based classes
        switch (step.Status)
        {
            case StepStatus.Active:
                classes.Add("active");
                break;
            case StepStatus.Completed:
                classes.Add("completed");
                classes.Add("success");
                break;
            case StepStatus.Warning:
                classes.Add("completed");
                classes.Add("warning");
                break;
            case StepStatus.Error:
                classes.Add("completed");
                classes.Add("error");
                break;
            case StepStatus.NotStarted:
                classes.Add("pending");
                break;
        }

        // Add animation class if it's the current step
        if (step.Status == StepStatus.Active)
        {
            classes.Add("animate-step");
        }

        return string.Join(" ", classes);
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
            try
            {
                await JsRuntime.InvokeVoidAsync("DropBearProgressBar.initialize", _progressId,
                    DotNetObjectReference.Create(this));
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing progress bar JavaScript");
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    public async Task MarkStepCompleteAsync(string stepName)
    {
        try
        {
            await _updateLock!.WaitAsync();
            await UpdateStepStatusAsync(stepName, StepStatus.Completed);

            var completedTasks = _steps.Count(step => step.Status == StepStatus.Completed);
            await UpdateProgressAsync(TaskProgress, completedTasks, _steps.Count);
        }
        finally
        {
            _updateLock?.Release();
        }
    }

    public async Task UpdateProgressAsync(double taskProgress, int completedTasks, int totalTasks)
    {
        if (_isDisposed)
        {
            Logger.Debug("Skipping progress update: Progress bar disposed.");
            return;
        }

        try
        {
            await _updateLock!.WaitAsync();

            TaskProgress = Math.Clamp(taskProgress, 0, 100);
            OverallProgress = totalTasks > 0
                ? Math.Clamp((double)completedTasks / totalTasks * 100, 0, 100)
                : 0;

            if (_isInitialized)
            {
                await ProgressChanged.InvokeAsync(OverallProgress);
                await UpdateStepDisplayAsync(GetCurrentStepIndex());
            }

            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            _updateLock?.Release();
        }
    }


    public async Task UpdateStepStatusAsync(string stepName, StepStatus status)
    {
        try
        {
            await _updateLock!.WaitAsync();

            var step = _steps.FirstOrDefault(s => s.Name == stepName);
            if (step != null)
            {
                step.Status = status;

                if (status == StepStatus.Completed)
                {
                    await StepCompleted.InvokeAsync(step);
                }

                if (_isInitialized)
                {
                    await UpdateStepDisplayAsync(GetCurrentStepIndex());
                }

                await InvokeAsync(StateHasChanged);
            }
        }
        finally
        {
            _updateLock?.Release();
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
        if (activeStep != null)
        {
            return _steps.ToList().IndexOf(activeStep);
        }

        return _steps.Count(s => s.Status == StepStatus.Completed);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            if (_isInitialized)
            {
                await SafeJsVoidInteropAsync("DropBearProgressBar.dispose", _progressId);
            }
        }
        catch (JSDisconnectedException)
        {
            Logger.Warning("JSInterop for DropBearProgressBar is already disposed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing progress bar JavaScript");
        }
        finally
        {
            _isDisposed = true;
            _updateLock?.Dispose();
            await base.DisposeAsync();
        }
    }
}
