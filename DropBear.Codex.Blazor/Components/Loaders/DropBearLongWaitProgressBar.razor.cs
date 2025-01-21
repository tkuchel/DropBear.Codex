#region

using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Loaders;

/// <summary>
///     A Blazor component for displaying a progress bar during long wait times, with an optional cancel button.
/// </summary>
public sealed partial class DropBearLongWaitProgressBar : DropBearComponentBase
{
    private const int DEFAULT_UPDATE_INTERVAL = 100;
    private const int DEFAULT_STEP_SIZE = 1;

    private readonly CancellationTokenSource _cts = new();
    private int _currentProgress;
    private bool _isCanceled;
    private bool _progressSet;
    private Timer? _timer;

    private bool IsIndeterminate => !_progressSet;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _currentProgress = Progress;
        Logger.Debug("Progress initialized: {Progress}%", _currentProgress);
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Progress != _currentProgress)
        {
            _progressSet = true;
            StartTimer();
        }

        if (Progress >= 100)
        {
            StopTimer();
            Logger.Debug("Progress complete");
        }
    }

    private void StartTimer()
    {
        _timer?.Dispose();
        _timer = new Timer(UpdateProgress, null, DEFAULT_UPDATE_INTERVAL, DEFAULT_UPDATE_INTERVAL);
        Logger.Debug("Progress timer started");
    }

    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.Debug("Progress timer stopped");
        }
    }

    private async void UpdateProgress(object? state)
    {
        if (_isCanceled || IsDisposed)
        {
            StopTimer();
            return;
        }

        try
        {
            await InvokeStateHasChangedAsync(() =>
            {
                if (_currentProgress < Progress)
                {
                    _currentProgress = Math.Min(_currentProgress + DEFAULT_STEP_SIZE, Progress);
                }
                else if (_currentProgress > Progress)
                {
                    _currentProgress = Math.Max(_currentProgress - DEFAULT_STEP_SIZE, Progress);
                }

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
    }

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

    public void Dispose()
    {
        _timer?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
        _ = DisposeAsync();
    }

    #region Parameters

    [Parameter] public string Title { get; set; } = "Please wait...";
    [Parameter] public string Message { get; set; } = "Processing your request...";
    [Parameter] public int Progress { get; set; }
    [Parameter] public bool ShowCancelButton { get; set; }
    [Parameter] public string CancelButtonText { get; set; } = "Cancel";
    [Parameter] public EventCallback OnCancel { get; set; }

    #endregion
}
