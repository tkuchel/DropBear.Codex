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
    private bool _isIndeterminate;
    private ObservableCollection<ProgressStep> _steps = new();

    public DropBearProgressBar()
    {
        _progressId = $"progress-{Guid.NewGuid():N}";
    }

    [Parameter]
    public ProgressBarType Type { get; set; } = ProgressBarType.Standard;

    [Parameter]
    public double Progress { get; set; }

    [Parameter]
    public IEnumerable<ProgressStep>? Steps { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public EventCallback<double> ProgressChanged { get; set; }

    [Parameter]
    public EventCallback<ProgressStep> StepCompleted { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (Type == ProgressBarType.Stepped && Steps?.Any() == true)
        {
            _steps = new ObservableCollection<ProgressStep>(Steps);
        }

        _isIndeterminate = Type == ProgressBarType.Indeterminate;
    }

    public async Task UpdateProgressAsync(double newProgress)
    {
        Progress = Math.Clamp(newProgress, 0, 100);
        await ProgressChanged.InvokeAsync(Progress);
        StateHasChanged();
    }

    public async Task UpdateStepStatusAsync(string stepName, StepStatus status)
    {
        var step = _steps.FirstOrDefault(s => s.Name == stepName);
        if (step != null)
        {
            step.Status = status;
            if (status == StepStatus.Completed)
            {
                await StepCompleted.InvokeAsync(step);
            }

            StateHasChanged();
        }
    }
}
