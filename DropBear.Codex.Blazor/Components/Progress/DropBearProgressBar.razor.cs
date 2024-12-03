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
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private bool _isDisposed;
    private bool _isIndeterminate;
    private bool _isInitialized;
    private ObservableCollection<ProgressStep> _steps = new();

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

    public async Task UpdateProgressAsync(double newProgress, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(DropBearProgressBar));
        }

        try
        {
            await _updateLock.WaitAsync(cancellationToken);

            if (!_isInitialized)
            {
                // Queue the update for the next render cycle
                await InvokeAsync(async () =>
                {
                    Progress = Math.Clamp(newProgress, 0, 100);
                    await ProgressChanged.InvokeAsync(Progress);
                    StateHasChanged();
                });
                return;
            }

            Progress = Math.Clamp(newProgress, 0, 100);
            await ProgressChanged.InvokeAsync(Progress);
            StateHasChanged();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public async Task UpdateStepStatusAsync(string stepName, StepStatus status)
    {
        try
        {
            await _updateLock.WaitAsync();

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
            _updateLock.Release();
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

            StateHasChanged();
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
