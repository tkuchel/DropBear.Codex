#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
/// A modern, accessible page-level alert component optimized for .NET 9+ and Blazor Server.
/// </summary>
public partial class DropBearPageAlert : DropBearComponentBase
{
    private readonly CancellationTokenSource _alertCts = new();
    private bool _isVisible;
    private double _progressPercentage = 100.0;
    private PageAlertType _type = PageAlertType.Info;

    /// <summary>
    /// The unique identifier for this alert instance.
    /// </summary>
    [Parameter]
    public string AlertId { get; set; } = $"alert-{Guid.NewGuid():N}";

    /// <summary>
    /// The title to display in the alert header.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// The message body of the alert, supports HTML.
    /// </summary>
    [Parameter]
    public string? Message { get; set; }

    /// <summary>
    /// The type of alert to display.
    /// </summary>
    [Parameter]
    public PageAlertType Type
    {
        get => _type;
        set => _type = value;
    }

    /// <summary>
    /// Whether the alert should remain visible until explicitly closed.
    /// </summary>
    [Parameter]
    public bool IsPermanent { get; set; }

    /// <summary>
    /// The duration in milliseconds to display the alert before auto-closing.
    /// </summary>
    [Parameter]
    public int? Duration { get; set; }

    /// <summary>
    /// Event callback when the alert is closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    /// CSS class based on alert type.
    /// </summary>
    protected string AlertTypeCssClass => Type switch
    {
        PageAlertType.Success => "alert--success",
        PageAlertType.Error => "alert--error",
        PageAlertType.Warning => "alert--warning",
        PageAlertType.Info => "alert--info",
        _ => "alert--info"
    };

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Show alert with a slight delay for smooth animation
        await Task.Delay(50, ComponentToken);
        _isVisible = true;
        StateHasChanged();
    }

    protected override async ValueTask InitializeComponentAsync()
    {
        try
        {
            if (!IsPermanent && Duration is > 0)
            {
                _ = StartProgressTimerAsync();
            }
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize alert component", ex);
            await CloseAlertAsync();
        }
    }

    /// <summary>
    /// Starts the progress timer using modern PeriodicTimer.
    /// </summary>
    private async Task StartProgressTimerAsync()
    {
        if (Duration is null or <= 0) return;

        var updateInterval = TimeSpan.FromMilliseconds(50); // Smooth 20fps updates
        var totalDuration = TimeSpan.FromMilliseconds(Duration.Value);
        var startTime = DateTime.UtcNow;

        using var timer = new PeriodicTimer(updateInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(_alertCts.Token))
            {
                var elapsed = DateTime.UtcNow - startTime;
                var progress = Math.Min(1.0, elapsed.TotalMilliseconds / totalDuration.TotalMilliseconds);

                _progressPercentage = (1.0 - progress) * 100.0;

                if (progress >= 1.0)
                {
                    await CloseAlertAsync();
                    break;
                }

                // Update UI every few ticks to balance smoothness and performance
                if (elapsed.TotalMilliseconds % 200 < updateInterval.TotalMilliseconds)
                {
                    await InvokeAsync(StateHasChanged);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when component is disposed
        }
    }

    /// <summary>
    /// Handles the close button click.
    /// </summary>
    private async Task HandleCloseClick()
    {
        await CloseAlertAsync();
    }

    /// <summary>
    /// Closes the alert with animation.
    /// </summary>
    private async Task CloseAlertAsync()
    {
        if (!_isVisible) return;

        try
        {
            await _alertCts.CancelAsync();

            _isVisible = false;
            await InvokeAsync(StateHasChanged);

            // Wait for animation to complete before invoking callback
            await Task.Delay(300, ComponentToken);

            if (OnClose.HasDelegate)
            {
                await OnClose.InvokeAsync();
            }
        }
        catch (Exception ex)
        {
            LogError("Error closing alert", ex);
        }
    }

    /// <summary>
    /// Handles animation end events.
    /// </summary>
    private async Task OnAnimationEnd()
    {
        if (!_isVisible && OnClose.HasDelegate)
        {
            await OnClose.InvokeAsync();
        }
    }

    /// <summary>
    /// Returns the appropriate icon for the alert type.
    /// </summary>
    private RenderFragment GetIcon() => Type switch
    {
        PageAlertType.Success => SuccessIcon,
        PageAlertType.Error => ErrorIcon,
        PageAlertType.Warning => WarningIcon,
        PageAlertType.Info => InfoIcon,
        _ => InfoIcon
    };

    private static readonly RenderFragment SuccessIcon = builder =>
    {
        builder.OpenElement(0, "svg");
        builder.AddAttribute(1, "viewBox", "0 0 24 24");
        builder.AddAttribute(2, "fill", "none");
        builder.AddAttribute(3, "stroke", "currentColor");
        builder.AddAttribute(4, "stroke-width", "2");
        builder.OpenElement(5, "path");
        builder.AddAttribute(6, "d", "M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z");
        builder.CloseElement();
        builder.CloseElement();
    };

    private static readonly RenderFragment ErrorIcon = builder =>
    {
        builder.OpenElement(0, "svg");
        builder.AddAttribute(1, "viewBox", "0 0 24 24");
        builder.AddAttribute(2, "fill", "none");
        builder.AddAttribute(3, "stroke", "currentColor");
        builder.AddAttribute(4, "stroke-width", "2");
        builder.OpenElement(5, "circle");
        builder.AddAttribute(6, "cx", "12");
        builder.AddAttribute(7, "cy", "12");
        builder.AddAttribute(8, "r", "10");
        builder.CloseElement();
        builder.OpenElement(9, "path");
        builder.AddAttribute(10, "d", "m15 9-6 6m0-6 6 6");
        builder.CloseElement();
        builder.CloseElement();
    };

    private static readonly RenderFragment WarningIcon = builder =>
    {
        builder.OpenElement(0, "svg");
        builder.AddAttribute(1, "viewBox", "0 0 24 24");
        builder.AddAttribute(2, "fill", "none");
        builder.AddAttribute(3, "stroke", "currentColor");
        builder.AddAttribute(4, "stroke-width", "2");
        builder.OpenElement(5, "path");
        builder.AddAttribute(6, "d",
            "m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3ZM12 9v4M12 17h.01");
        builder.CloseElement();
        builder.CloseElement();
    };

    private static readonly RenderFragment InfoIcon = builder =>
    {
        builder.OpenElement(0, "svg");
        builder.AddAttribute(1, "viewBox", "0 0 24 24");
        builder.AddAttribute(2, "fill", "none");
        builder.AddAttribute(3, "stroke", "currentColor");
        builder.AddAttribute(4, "stroke-width", "2");
        builder.OpenElement(5, "circle");
        builder.AddAttribute(6, "cx", "12");
        builder.AddAttribute(7, "cy", "12");
        builder.AddAttribute(8, "r", "10");
        builder.CloseElement();
        builder.OpenElement(9, "path");
        builder.AddAttribute(10, "d", "m9 12 2 2 4-4");
        builder.CloseElement();
        builder.CloseElement();
    };


    protected override async ValueTask DisposeAsyncCore()
    {
        await _alertCts.CancelAsync();
        _alertCts.Dispose();
        await base.DisposeAsyncCore();
    }
}
