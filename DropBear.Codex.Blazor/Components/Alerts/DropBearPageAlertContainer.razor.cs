#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A container component for displaying page alerts.
/// </summary>
public partial class DropBearPageAlertContainer : DropBearComponentBase, IDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearPageAlertContainer>();

    /// <summary>
    ///     Disposes of the alert service subscription.
    /// </summary>
    public void Dispose()
    {
        try
        {
            AlertService.OnChange -= HandleAlertChange;
            Logger.Debug("Alert service subscription disposed successfully.");
        }
        catch (Exception ex)
        {
            // Log the exception if necessary
            Logger.Error(ex, "Error occurred while disposing of the alert service subscription.");
        }
    }

    /// <summary>
    ///     Subscribes to the alert service on initialization.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        AlertService.OnChange += HandleAlertChange;
        Logger.Debug("Alert service subscription initialized.");
    }

    /// <summary>
    ///     Handles changes in the alert service.
    /// </summary>
    private void HandleAlertChange(object? sender, EventArgs e)
    {
        _ = InvokeAsync(StateHasChanged);
        Logger.Debug("Alert service state changed; UI will be updated.");
    }
}
