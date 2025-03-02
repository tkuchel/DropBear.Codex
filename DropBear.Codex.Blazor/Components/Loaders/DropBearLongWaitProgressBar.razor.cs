#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A Blazor component for displaying a progress bar during long wait times,
///     with an optional cancel button.
/// </summary>
public sealed partial class DropBearLongWaitProgressBar : DropBearComponentBase
{
    #region Progress Update

    /// <summary>
    ///     Timer callback that updates the displayed progress toward the target value.
    ///     Uses an interlocked flag to prevent overlapping invocations.
    /// </summary>
    /// <param name="state">Unused state parameter.</param>
    private async void UpdateProgress(object? state)
    {
        // If cancellation was requested or component is disposed, stop the timer.
        if (_isCanceled || IsDisposed)
        {
            StopTimer();
            return;
        }

        // Prevent reentrant calls using atomic operations for thread safety
        if (Interlocked.CompareExchange(ref _updateInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            // Use a helper (from DropBearComponentBase) to queue state updates on the UI thread.
            await QueueStateHasChangedAsync(() =>
            {
                // Gradually adjust _currentProgress toward Progress.
                if (_currentProgress < Progress)
                {
                    _currentProgress = Math.Min(_currentProgress + DEFAULT_STEP_SIZE, Progress);
                }
                else if (_currentProgress > Progress)
                {
                    _currentProgress = Math.Max(_currentProgress - DEFAULT_STEP_SIZE, Progress);
                }

                // Once the progress matches the target, stop the timer.
                if (_currentProgress == Progress)
                {
                    StopTimer();
                }

                Logger.Debug("Progress updated: {Progress}%", _currentProgress);
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating progress");
        }
        finally
        {
            // Release the update lock.
            Interlocked.Exchange(ref _updateInProgress, 0);
        }
    }

    #endregion

    #region Cancellation

    /// <summary>
    ///     Cancels the progress and invokes the OnCancel callback, if set.
    /// </summary>
    private async Task CancelAsync()
    {
        if (_isCanceled || !OnCancel.HasDelegate)
        {
            return;
        }

        try
        {
            _isCanceled = true;
            StopTimer();
            await OnCancel.InvokeAsync();
            Logger.Debug("Progress canceled");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error canceling progress");
        }
    }

    #endregion

    #region Disposal

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        // Dispose of timer and cancellation resources.
        _ = _timer?.DisposeAsync();
        _timer = null;

        try
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore if already disposed
        }

        // Trigger any asynchronous disposal in the base class.
        await base.DisposeAsyncCore();
    }

    #endregion

    #region Constants and Fields

    // Constants used for timer update intervals and progress step size.
    private const int DEFAULT_UPDATE_INTERVAL = 100;
    private const int DEFAULT_STEP_SIZE = 1;

    // Cancellation token source for overall component cancellation.
    private readonly CancellationTokenSource _cts = new();

    // Current displayed progress.
    private int _currentProgress;

    // Backing fields for parameters to detect changes
    private string _title = "Please wait...";
    private string _message = "Processing your request...";
    private int _progress;
    private bool _showCancelButton;
    private string _cancelButtonText = "Cancel";

    // Flag indicating that progress was set (i.e. determinate mode).
    private bool _progressSet;

    // Flag to indicate that cancellation was requested.
    private bool _isCanceled;

    // Timer used for updating the displayed progress.
    private Timer? _timer;

    // Flag used to prevent reentrant calls from the timer callback.
    private int _updateInProgress;

    /// <summary>
    ///     Returns true when progress has not been explicitly set.
    /// </summary>
    private bool IsIndeterminate => !_progressSet;

    // Flag to track if component should render
    private bool _shouldRender = true;

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Controls whether the component should render, optimizing for performance.
    /// </summary>
    /// <returns>True if the component should render, false otherwise.</returns>
    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _currentProgress = Progress;
        Logger.Debug("Progress initialized: {Progress}%", _currentProgress);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        // If progress has changed, mark determinate mode and start updating.
        if (Progress != _currentProgress)
        {
            _progressSet = true;
            StartTimer();
            _shouldRender = true;
        }

        // When progress reaches 100%, stop the timer.
        if (Progress >= 100)
        {
            StopTimer();
            Logger.Debug("Progress complete");
            _shouldRender = true;
        }
    }

    #endregion

    #region Timer Management

    /// <summary>
    ///     Starts or restarts the update timer.
    /// </summary>
    private void StartTimer()
    {
        // Dispose any existing timer before creating a new one.
        _timer?.Dispose();
        _timer = new Timer(UpdateProgress, null, DEFAULT_UPDATE_INTERVAL, DEFAULT_UPDATE_INTERVAL);
        Logger.Debug("Progress timer started");
    }

    /// <summary>
    ///     Stops the update timer.
    /// </summary>
    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.Debug("Progress timer stopped");
        }
    }

    #endregion

    #region Parameters

    /// <summary>
    ///     The title to display above the progress bar.
    /// </summary>
    [Parameter]
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     The message displayed alongside the progress bar.
    /// </summary>
    [Parameter]
    public string Message
    {
        get => _message;
        set
        {
            if (_message != value)
            {
                _message = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     The target progress value (0-100).
    /// </summary>
    [Parameter]
    public int Progress
    {
        get => _progress;
        set
        {
            if (_progress != value)
            {
                _progress = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Whether to show a cancel button.
    /// </summary>
    [Parameter]
    public bool ShowCancelButton
    {
        get => _showCancelButton;
        set
        {
            if (_showCancelButton != value)
            {
                _showCancelButton = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     The text for the cancel button.
    /// </summary>
    [Parameter]
    public string CancelButtonText
    {
        get => _cancelButtonText;
        set
        {
            if (_cancelButtonText != value)
            {
                _cancelButtonText = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Callback invoked when cancellation is requested.
    /// </summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    #endregion
}
