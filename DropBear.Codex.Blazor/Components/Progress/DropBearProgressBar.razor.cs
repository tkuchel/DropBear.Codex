#region

using System.Collections.ObjectModel;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Progress;

public sealed partial class DropBearProgressBar : DropBearComponentBase
{
    private readonly string _progressId;
    private readonly SemaphoreSlim? _updateLock = new(1, 1);
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
            if (_taskProgress != value)
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
            if (_overallProgress != value)
            {
                _overallProgress = value;
                InvokeAsync(StateHasChanged);
            }
        }
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
            _isInitialized = true;
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    public async Task MarkStepCompleteAsync(string stepName)
    {
        await UpdateStepStatusAsync(stepName, StepStatus.Completed);
        var completedTasks = _steps.Count(step => step.Status == StepStatus.Completed);
        await UpdateProgressAsync(TaskProgress, completedTasks, _steps.Count);
    }


    public async Task UpdateProgressAsync(double taskProgress, int completedTasks, int totalTasks)
    {
        try
        {
            await _updateLock?.WaitAsync()!;

            if (!_isInitialized)
            {
                await InvokeAsync(async () =>
                {
                    TaskProgress = Math.Clamp(taskProgress, 0, 100);
                    OverallProgress = Math.Clamp((double)completedTasks / totalTasks * 100, 0, 100);
                    await InvokeAsync(StateHasChanged);
                });
                return;
            }

            TaskProgress = Math.Clamp(taskProgress, 0, 100);
            OverallProgress = Math.Clamp((double)completedTasks / totalTasks * 100, 0, 100);
            await ProgressChanged.InvokeAsync(OverallProgress);
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
            await _updateLock?.WaitAsync()!;

            if (!_isInitialized)
            {
                await InvokeAsync(async () =>
                {
                    await UpdateStepInternalAsync(stepName, status);
                });
                return;
            }

            await UpdateStepInternalAsync(stepName, status);
        }
        finally
        {
            _updateLock?.Release();
        }
    }

    private async Task UpdateStepInternalAsync(string stepName, StepStatus status)
    {
        var step = _steps.FirstOrDefault(s => s.Name == stepName);
        if (step != null)
        {
            step.Status = status;
            if (status == StepStatus.Completed)
            {
                await StepCompleted.InvokeAsync(step);
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_updateLock is not null)
        {
            _updateLock.Dispose();
        }

        await base.DisposeAsync();
    }
}
