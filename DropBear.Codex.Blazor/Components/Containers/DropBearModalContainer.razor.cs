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
    private string customHeight = "auto"; // Default height
    private string customWidth = "auto"; // Default width
    private string modalTransitionClass = "enter"; // Controls enter/leave animations
    private string transitionDuration = "0.3s"; // Default transition duration

    /// <summary>
    ///     Cleanup on component disposal, ensuring event unsubscription.
    /// </summary>
    public void Dispose()
    {
        try
        {
            ModalService.OnChange -= StateHasChanged;
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
                    customWidth = width?.ToString() ?? "auto";
                }

                if (ModalService.CurrentParameters.TryGetValue("CustomHeight", out var height))
                {
                    customHeight = height?.ToString() ?? "auto";
                }

                if (ModalService.CurrentParameters.TryGetValue("TransitionDuration", out var duration))
                {
                    transitionDuration = duration?.ToString() ?? "0.3s";
                }
            }

            Log.Debug(
                "Modal container initialized with custom dimensions: Width = {Width}, Height = {Height}, Transition Duration = {Duration}",
                customWidth, customHeight, transitionDuration);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred during initialization of DropBearModalContainer.");
        }
    }

    /// <summary>
    ///     Handles clicking outside the modal to trigger its closure, if allowed.
    /// </summary>
    private async Task HandleOutsideClick()
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

    /// <summary>
    ///     Starts the transition animation when opening or closing the modal.
    /// </summary>
    /// <param name="isOpening">Indicates if the modal is opening (true) or closing (false).</param>
    private async Task StartTransition(bool isOpening)
    {
        try
        {
            modalTransitionClass = isOpening ? "enter" : "leave";
            StateHasChanged();

            // Delay to match the CSS transition duration (ensuring smooth animations).
            await Task.Delay(300);
            Log.Debug("Modal transition {Transition} started.", isOpening ? "enter" : "leave");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred during modal transition.");
        }
    }
}
