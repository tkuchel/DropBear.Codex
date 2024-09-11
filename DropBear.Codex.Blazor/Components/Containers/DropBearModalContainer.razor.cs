#region

using DropBear.Codex.Blazor.Components.Bases;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container for displaying modals, with customizable width, height, and transitions.
/// </summary>
public partial class DropBearModalContainer : DropBearComponentBase, IDisposable
{
    private string _customHeight = "auto"; // Default height
    private string _customWidth = "auto"; // Default width
    private string _modalTransitionClass = "enter"; // Controls enter/leave animations
    private string _transitionDuration = "0.3s"; // Default transition duration

    /// <summary>
    ///     Cleanup on component disposal, ensuring event unsubscription.
    /// </summary>
    public void Dispose()
    {
        try
        {
            ModalService.OnChange -= StateHasChanged;
            GC.SuppressFinalize(this);
            Log.Debug("Unsubscribed from ModalService OnChange event.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while unsubscribing from ModalService during disposal.");
        }
    }

    /// <summary>
    ///     Subscribes to the ModalService's OnChange event during initialization.
    /// </summary>
    protected override void OnInitialized()
    {
        try
        {
            ModalService.OnChange += StateHasChanged;

            if (ModalService.CurrentParameters != null)
            {
                // Retrieve custom width, height, and transition duration if specified
                if (ModalService.CurrentParameters.TryGetValue("CustomWidth", out var width))
                {
                    _customWidth = width.ToString() ?? "auto";
                }

                if (ModalService.CurrentParameters.TryGetValue("CustomHeight", out var height))
                {
                    _customHeight = height.ToString() ?? "auto";
                }

                if (ModalService.CurrentParameters.TryGetValue("TransitionDuration", out var duration))
                {
                    _transitionDuration = duration.ToString() ?? "0.3s";
                }
            }

            Log.Debug(
                "Modal container initialized with custom dimensions: Width = {Width}, Height = {Height}, Transition Duration = {Duration}",
                _customWidth, _customHeight, _transitionDuration);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred during initialization of DropBearModalContainer.");
        }
    }

    /// <summary>
    ///     Handles clicking outside the modal to trigger its closure, if allowed.
    /// </summary>
    private Task HandleOutsideClick()
    {
        try
        {
            // Handle modal closure via outside click if not sticky.
            ModalService.Close();
            Log.Debug("Modal closed via outside click.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling outside click to close modal.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Manually triggers the modal close operation.
    /// </summary>
    public void TriggerClose()
    {
        try
        {
            ModalService.Close();
            Log.Debug("Modal manually closed.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error triggering modal close.");
        }
    }
}
