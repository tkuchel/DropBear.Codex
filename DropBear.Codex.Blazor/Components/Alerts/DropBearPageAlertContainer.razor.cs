#region

using DropBear.Codex.Blazor.Components.Bases;

#endregion

namespace DropBear.Codex.Blazor.Components.Alerts;

/// <summary>
///     A container component for displaying page alerts.
/// </summary>
public partial class DropBearPageAlertContainer : DropBearComponentBase, IDisposable
{
    /// <summary>
    ///     Disposes of the alert service subscription.
    /// </summary>
    public void Dispose()
    {
        try
        {
            AlertService.OnChange -= HandleAlertChange;
        }
        catch (Exception ex)
        {
            // Log the exception if necessary
        }
    }

    /// <summary>
    ///     Subscribes to the alert service on initialization.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        AlertService.OnChange += HandleAlertChange;
    }

    /// <summary>
    ///     Handles changes in the alert service.
    /// </summary>
    private void HandleAlertChange(object? sender, EventArgs e)
    {
        _ = InvokeAsync(StateHasChanged);
    }
}
