#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A Blazor component for displaying a progress bar for long wait times.
/// </summary>
public sealed partial class LongWaitProgressBar : DropBearComponentBase, IDisposable
{
    private const int UpdateInterval = 100; // Interval in milliseconds
    private const int StepSize = 1; // Step size for progress increment
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<LongWaitProgressBar>();

    private int _currentProgress;
    private bool _isCanceled;
    private bool _isIndeterminate = true;
    private Timer? _timer;

    /// <summary>
    ///     The title displayed above the progress bar.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = "Please wait...";

    /// <summary>
    ///     The message displayed below the title.
    /// </summary>
    [Parameter]
    public string Message { get; set; } = "Processing your request...";

    /// <summary>
    ///     The current progress percentage (0-100).
    /// </summary>
    [Parameter]
    public int Progress { get; set; }

    /// <summary>
    ///     Whether the cancel button should be shown.
    /// </summary>
    [Parameter]
    public bool ShowCancelButton { get; set; }

    /// <summary>
    ///     The text displayed on the cancel button.
    /// </summary>
    [Parameter]
    public string CancelButtonText { get; set; } = "Cancel";

    /// <summary>
    ///     The callback invoked when the user cancels the operation.
    /// </summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>
    ///     The callback invoked when the progress changes. Includes the operation name and progress percentage.
    /// </summary>
    [Parameter]
    public EventCallback<(string OperationName, int ProgressPercentage)> ProgressChanged { get; set; }

    public void Dispose()
    {
        _timer?.Dispose();
        Logger.Information("LongWaitProgressBar disposed.");
    }

    protected override void OnInitialized()
    {
        _currentProgress = Progress;
        _timer = new Timer(UpdateProgress, null, Timeout.Infinite, UpdateInterval);
        Logger.Information("LongWaitProgressBar initialized with initial progress of {Progress}%", _currentProgress);
    }

    protected override void OnParametersSet()
    {
        if (_isIndeterminate)
        {
            if (Progress > 0)
            {
                _isIndeterminate = false;
                StartTimer();
            }
        }
        else if (Progress != _currentProgress)
        {
            StartTimer();
        }

        if (_currentProgress >= 100)
        {
            StopTimer();
            Logger.Information("Progress reached 100%.");
        }
    }

    private void StartTimer()
    {
        _timer?.Change(0, UpdateInterval); // Start or restart the timer
        Logger.Debug("Progress timer started or restarted.");
    }

    private void StopTimer()
    {
        _timer?.Change(Timeout.Infinite, UpdateInterval); // Stop the timer
        Logger.Debug("Progress timer stopped.");
    }

    private void UpdateProgress(object? state)
    {
        if (_isCanceled)
        {
            StopTimer();
            return;
        }

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
            StopTimer();
        }

        Logger.Debug("Progress updated to {Progress}%", _currentProgress);

        // Notify parent component of progress changes
        _ = ProgressChanged.InvokeAsync((Title, _currentProgress));

        _ = InvokeAsync(StateHasChanged);
    }

    private async Task Cancel()
    {
        if (!_isCanceled)
        {
            _isCanceled = true;
            StopTimer();
            Logger.Information("Cancel button clicked. Progress bar canceled.");

            if (OnCancel.HasDelegate)
            {
                await OnCancel.InvokeAsync();
            }
        }
    }
}
