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
    private const int DefaultUpdateInterval = 100; // Interval in milliseconds
    private const int DefaultStepSize = 1; // Step size for progress increment
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<LongWaitProgressBar>();
    private bool _isCanceled;
    private bool _progressSet;

    private Timer? _timer;

    /// <summary>
    ///     Gets or sets the title displayed above the progress bar.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = "Please wait...";

    /// <summary>
    ///     Gets or sets the message displayed below the title.
    /// </summary>
    [Parameter]
    public string Message { get; set; } = "Processing your request...";

    /// <summary>
    ///     Gets or sets the current progress percentage (0-100).
    /// </summary>
    [Parameter]
    public int Progress { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the cancel button should be shown.
    /// </summary>
    [Parameter]
    public bool ShowCancelButton { get; set; }

    /// <summary>
    ///     Gets or sets the text displayed on the cancel button.
    /// </summary>
    [Parameter]
    public string CancelButtonText { get; set; } = "Cancel";

    /// <summary>
    ///     Gets or sets the callback invoked when the user cancels the operation.
    /// </summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the progress bar is indeterminate.
    /// </summary>
    private bool IsIndeterminate => !_progressSet;

    /// <summary>
    ///     Gets the current progress percentage.
    /// </summary>
    private int CurrentProgress { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer?.Dispose();
        Logger.Debug("LongWaitProgressBar disposed.");
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        CurrentProgress = Progress;
        Logger.Debug("LongWaitProgressBar initialized with initial progress of {Progress}%", CurrentProgress);
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Progress != CurrentProgress)
        {
            _progressSet = true;
            StartTimer();
        }

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
        // Ensure UI updates are invoked on the correct Blazor render thread
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
        if (!_isCanceled)
        {
            _isCanceled = true;
            StopTimer();
            Logger.Debug("Cancel button clicked. Progress bar canceled.");

            if (OnCancel.HasDelegate)
            {
                await OnCancel.InvokeAsync();
            }
        }
    }
}
