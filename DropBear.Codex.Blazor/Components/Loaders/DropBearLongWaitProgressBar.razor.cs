#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A Blazor component for displaying a progress bar during long wait times, with an optional cancel button.
/// </summary>
public sealed partial class DropBearLongWaitProgressBar : DropBearComponentBase, IDisposable
{
    private const int DefaultUpdateInterval = 100; // ms
    private const int DefaultStepSize = 1; // increment step

    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearLongWaitProgressBar>();
    private bool _isCanceled;
    private bool _progressSet;

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
    ///     The target progress percentage (0-100).
    /// </summary>
    [Parameter]
    public int Progress { get; set; }

    /// <summary>
    ///     If true, shows a cancel button.
    /// </summary>
    [Parameter]
    public bool ShowCancelButton { get; set; }

    /// <summary>
    ///     The text displayed on the cancel button.
    /// </summary>
    [Parameter]
    public string CancelButtonText { get; set; } = "Cancel";

    /// <summary>
    ///     Callback invoked if the user clicks 'cancel'.
    /// </summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>
    ///     Indicates if the progress bar should be treated as indeterminate (no progress value set).
    /// </summary>
    private bool IsIndeterminate => !_progressSet;

    /// <summary>
    ///     The current displayed progress.
    /// </summary>
    private int CurrentProgress { get; set; }

    /// <summary>
    ///     Disposes any managed resources (timer).
    /// </summary>
    public void Dispose()
    {
        _timer?.Dispose();
        Logger.Debug("DropBearLongWaitProgressBar disposed.");
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        CurrentProgress = Progress;
        Logger.Debug("DropBearLongWaitProgressBar initialized with initial progress: {Progress}%", CurrentProgress);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // If the external 'Progress' param changes, set _progressSet and start the timer.
        if (Progress != CurrentProgress)
        {
            _progressSet = true;
            StartTimer();
        }

        // If progress is maxed, stop the timer.
        if (Progress >= 100)
        {
            StopTimer();
            Logger.Debug("Progress reached 100%.");
        }
    }

    private void StartTimer()
    {
        if (_timer == null)
        {
            _timer = new Timer(UpdateProgress, null, DefaultUpdateInterval, DefaultUpdateInterval);
            Logger.Debug("Progress timer started.");
        }
        else
        {
            _timer.Change(DefaultUpdateInterval, DefaultUpdateInterval);
            Logger.Debug("Progress timer restarted.");
        }
    }

    private void StopTimer()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        Logger.Debug("Progress timer stopped.");
    }

    private void UpdateProgress(object? state)
    {
        // Must call InvokeAsync to update UI from the UI thread.
        InvokeAsync(() =>
        {
            if (_isCanceled)
            {
                StopTimer();
                return;
            }

            if (CurrentProgress < Progress)
            {
                CurrentProgress = Math.Min(CurrentProgress + DefaultStepSize, Progress);
            }
            else if (CurrentProgress > Progress)
            {
                CurrentProgress = Math.Max(CurrentProgress - DefaultStepSize, Progress);
            }

            if (CurrentProgress == Progress)
            {
                StopTimer();
            }

            Logger.Debug("Progress updated to {Progress}%", CurrentProgress);
            StateHasChanged();
        });
    }

    private async Task CancelAsync()
    {
        if (_isCanceled)
        {
            return;
        }

        _isCanceled = true;
        StopTimer();
        Logger.Debug("Cancel button clicked, progress bar canceled.");

        if (OnCancel.HasDelegate)
        {
            await OnCancel.InvokeAsync();
        }
    }
}
