#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A Blazor component for displaying a progress bar for long wait times.
/// </summary>
public sealed partial class LongWaitProgressBar : DropBearComponentBase, IDisposable
{
    private const int UpdateInterval = 100; // Interval in milliseconds
    private const int StepSize = 1; // Step size for progress increment

    private int _currentProgress;
    private bool _isIndeterminate = true;
    private Timer? _timer;

    [Parameter] public string Title { get; set; } = "Please wait...";
    [Parameter] public string Message { get; set; } = "Please wait while we process your request.";
    [Parameter] public int Progress { get; set; }
    [Parameter] public bool ShowCancelButton { get; set; }
    [Parameter] public string CancelButtonText { get; set; } = "Cancel";
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public EventCallback<(string OperationName, int ProgressPercentage)> ProgressChanged { get; set; }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    protected override void OnInitialized()
    {
        _currentProgress = Progress;
        _timer = new Timer(UpdateProgress, null, Timeout.Infinite, UpdateInterval);
    }

    protected override void OnParametersSet()
    {
        if (_isIndeterminate)
        {
            if (Progress > 0)
            {
                _isIndeterminate = false;
                _timer?.Change(0, UpdateInterval); // Start or restart the timer
            }
        }
        else if (Progress != _currentProgress)
        {
            _timer?.Change(0, UpdateInterval); // Start or restart the timer
        }
    }

    private void UpdateProgress(object? state)
    {
        if (_currentProgress < Progress)
        {
            _currentProgress = Math.Min(_currentProgress + StepSize, Progress);
        }
        else if (_currentProgress > Progress)
        {
            _currentProgress = Math.Max(_currentProgress - StepSize, Progress);
        }

        if (_currentProgress == Progress)
        {
            _timer?.Change(Timeout.Infinite, UpdateInterval); // Stop the timer
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void Cancel()
    {
        _ = OnCancel.InvokeAsync();
    }
}
