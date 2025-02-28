#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using System.Timers;
using Timer = System.Timers.Timer;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A page-level alert component for displaying success/error/warning/info messages.
/// </summary>
public sealed partial class DropBearPageAlert : DropBearComponentBase
{
    private string _progressBarStyle = string.Empty;
    private Timer? _closeTimer;
    private Timer? _progressTimer;
    private readonly int _progressUpdateInterval = 100; // ms
    private int _elapsedTime = 0;

    /// <summary>
    /// The unique identifier for this alert instance.
    /// </summary>
    [Parameter] public string AlertId { get; set; } = $"alert-{Guid.NewGuid():N}";

    /// <summary>
    /// The title to display in the alert header.
    /// </summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>
    /// The message body of the alert, supports HTML.
    /// </summary>
    [Parameter] public string? Message { get; set; }

    /// <summary>
    /// The type of alert to display (Success, Error, Warning, Info).
    /// </summary>
    [Parameter] public PageAlertType Type { get; set; } = PageAlertType.Info;

    /// <summary>
    /// Whether the alert should remain visible until explicitly closed.
    /// </summary>
    [Parameter] public bool IsPermanent { get; set; }

    /// <summary>
    /// The duration in milliseconds to display the alert before auto-closing.
    /// Only applies when IsPermanent is false.
    /// </summary>
    [Parameter] public int? Duration { get; set; }

    /// <summary>
    /// Event callback when the alert is closed either by timer or user action.
    /// </summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>
    /// CSS class to apply based on alert type.
    /// </summary>
    protected string AlertTypeCssClass => Type switch
    {
        PageAlertType.Success => "success",
        PageAlertType.Error => "error",
        PageAlertType.Warning => "warning",
        PageAlertType.Info => "info",
        _ => "info"
    };

    /// <summary>
    /// Generates SVG path data for the alert icon based on type.
    /// </summary>
    protected string GetIconPath() => Type switch
    {
        PageAlertType.Success => "<path d=\"M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z\"></path>",
        PageAlertType.Error => "<path d=\"M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z\"></path>",
        PageAlertType.Warning => "<path d=\"M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z\"></path>",
        PageAlertType.Info => "<path d=\"M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z\"></path>",
        _ => "<path d=\"M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z\"></path>"
    };

    /// <summary>
    /// Initiates alert setup after the component renders.
    /// </summary>
    protected override Task InitializeComponentAsync()
    {
        try
        {
            if (!IsPermanent && Duration.HasValue && Duration.Value > 0)
            {
                InitializeTimers(Duration.Value);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize alert component");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Sets up the timer for alert auto-closing and progress visualization.
    /// </summary>
    /// <param name="duration">Duration in milliseconds</param>
    private void InitializeTimers(int duration)
    {
        // Dispose any existing timers
        _closeTimer?.Dispose();
        _progressTimer?.Dispose();

        // Main timer to close the alert
        _closeTimer = new Timer(duration);
        _closeTimer.AutoReset = false;
        _closeTimer.Elapsed += async (sender, args) => await CloseAlertAsync();

        // Progress timer for visual feedback
        _progressTimer = new Timer(_progressUpdateInterval);
        _progressTimer.AutoReset = true;
        _progressTimer.Elapsed += UpdateProgressBar;

        // Start timers
        _elapsedTime = 0;
        _progressTimer.Start();
        _closeTimer.Start();
    }

    /// <summary>
    /// Updates the progress bar visualization.
    /// </summary>
    private void UpdateProgressBar(object? sender, ElapsedEventArgs e)
    {
        if (Duration is null || Duration.Value <= 0) return;

        _elapsedTime += _progressUpdateInterval;
        var percentage = Math.Min(100, (_elapsedTime * 100) / Duration.Value);
        var scaleX = 1 - (percentage / 100.0);

        // Only schedule UI updates if there's a meaningful change
        if (percentage % 5 == 0 || percentage >= 100)
        {
            _ = InvokeAsync(() =>
            {
                _progressBarStyle = $"transform: scaleX({scaleX:F2});";
                StateHasChanged();
            });
        }
    }

    /// <summary>
    /// Handles user-initiated close request.
    /// </summary>
    protected async Task RequestClose()
    {
        await CloseAlertAsync();
    }

    /// <summary>
    /// Performs alert closure operations.
    /// </summary>
    private async Task CloseAlertAsync()
    {
        // Avoid multiple closures
        if (_closeTimer == null) return;

        // Stop and dispose timers
        _progressTimer?.Stop();
        _closeTimer?.Stop();
        _progressTimer?.Dispose();
        _closeTimer?.Dispose();
        _progressTimer = null;
        _closeTimer = null;

        // Invoke the close callback
        try
        {
            await OnClose.InvokeAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in alert close callback");
        }
    }

    /// <summary>
    /// Ensures timers are properly disposed when the component is removed.
    /// </summary>
    protected override ValueTask DisposeAsyncCore()
    {
        _progressTimer?.Dispose();
        _closeTimer?.Dispose();
        _progressTimer = null;
        _closeTimer = null;
        return base.DisposeAsyncCore();
    }
}
