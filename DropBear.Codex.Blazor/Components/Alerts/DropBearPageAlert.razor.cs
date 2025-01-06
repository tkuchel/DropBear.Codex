#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A page-level alert component for displaying success/error/warning/info messages.
/// </summary>
public sealed partial class DropBearPageAlert : DropBearComponentBase
{
    // Stores the SVG path data for each alert type.
    private static readonly Dictionary<PageAlertType, string> IconPaths = new()
    {
        { PageAlertType.Success, "<path d=\"M20 6L9 17L4 12\"></path>" },
        {
            PageAlertType.Error,
            "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle><path d=\"M15 9l-6 6M9 9l6 6\"></path>"
        },
        { PageAlertType.Warning, "<path d=\"M12 9v2m0 4h.01\"></path><path d=\"M12 5l7 13H5l7-13z\"></path>" },
        {
            PageAlertType.Info,
            "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle><path d=\"M12 16v-4m0-4h.01\"></path>"
        }
    };

    /// <summary>
    ///     The unique identifier for this alert, used as the HTML element ID.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string? AlertId { get; set; }

    /// <summary>
    ///     The alert title text to display in bold.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string? Title { get; set; }

    /// <summary>
    ///     The main message text displayed in the alert.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string? Message { get; set; }

    /// <summary>
    ///     Specifies the alert type (Success, Error, Warning, Info).
    /// </summary>
    [Parameter]
    public PageAlertType Type { get; set; } = PageAlertType.Info;

    /// <summary>
    ///     If set to true, the alert remains visible indefinitely (no progress bar).
    /// </summary>
    [Parameter]
    public bool IsPermanent { get; set; }

    /// <summary>
    ///     Optional duration in milliseconds for how long the alert should remain visible.
    /// </summary>
    [Parameter]
    public int? Duration { get; set; }

    /// <summary>
    ///     Callback invoked when the alert is closed by the user.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    ///     Derives the CSS class for the alert's type (e.g., "success", "error", etc.).
    /// </summary>
    private string AlertTypeCssClass => Type.ToString().ToLowerInvariant();

    /// <summary>
    ///     A convenience property returning <see cref="AlertId" /> non-null.
    /// </summary>
    private string Id => AlertId!;

    /// <summary>
    ///     Invoked when the user clicks the close button; calls the OnClose callback.
    /// </summary>
    private async Task RequestClose()
    {
        try
        {
            await OnClose.InvokeAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error closing page alert {AlertId}", AlertId);
        }
    }

    /// <summary>
    ///     Returns the SVG path(s) for the icon matching the current alert type.
    /// </summary>
    private string GetIconPath()
    {
        return IconPaths.TryGetValue(Type, out var path) ? path : string.Empty;
    }

    /// <summary>
    ///     Validates that required parameters are set.
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (string.IsNullOrWhiteSpace(AlertId))
        {
            throw new ArgumentException("AlertId cannot be null or empty.", nameof(AlertId));
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Title cannot be null or empty.", nameof(Title));
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(Message));
        }
    }
}
