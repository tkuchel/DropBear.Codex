#region

using DropBear.Codex.Blazor.Components.Bases;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container for displaying modals, with customizable width, height, and transitions.
/// </summary>
public sealed partial class DropBearModalContainer : DropBearComponentBase, IDisposable
{
    private readonly string _modalTransitionClass = "enter"; // Controls enter/leave animations
    private string _customHeight = "auto"; // Default height
    private string _customWidth = "auto"; // Default width
    private bool _isSubscribed;
    private string _transitionDuration = "0.3s"; // Default transition duration

    /// <summary>
    ///     Cleanup on component disposal, ensuring event unsubscription.
    /// </summary>
    public void Dispose()
    {
        UnsubscribeFromModalService();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Subscribes to the ModalService's OnChange event during initialization.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeToModalService();
    }


    /// <summary>
    ///     Subscribes to the ModalService OnChange event.
    /// </summary>
    private void SubscribeToModalService()
    {
        if (!_isSubscribed)
        {
            ModalService.OnChange += StateHasChanged;
            _isSubscribed = true;
            SetCustomParameters();
            Log.Debug("Subscribed to ModalService OnChange event.");
        }
    }

    /// <summary>
    ///     Unsubscribes from the ModalService OnChange event.
    /// </summary>
    private void UnsubscribeFromModalService()
    {
        if (_isSubscribed)
        {
            ModalService.OnChange -= StateHasChanged;
            _isSubscribed = false;
            Log.Debug("Unsubscribed from ModalService OnChange event.");
        }
    }

    /// <summary>
    ///     Retrieves custom parameters for width, height, and transition duration.
    /// </summary>
    private void SetCustomParameters()
    {
        if (ModalService.CurrentParameters != null)
        {
            // Retrieve custom width, height, and transition duration if specified
            if (ModalService.CurrentParameters.TryGetValue("CustomWidth", out var width))
            {
                _customWidth = width?.ToString() ?? "auto";
            }

            if (ModalService.CurrentParameters.TryGetValue("CustomHeight", out var height))
            {
                _customHeight = height?.ToString() ?? "auto";
            }

            if (ModalService.CurrentParameters.TryGetValue("TransitionDuration", out var duration))
            {
                _transitionDuration = duration?.ToString() ?? "0.3s";
            }
        }

        Log.Debug(
            "Modal container initialized with custom dimensions: Width = {Width}, Height = {Height}, Transition Duration = {Duration}",
            _customWidth, _customHeight, _transitionDuration);
    }

    /// <summary>
    ///     Handles clicking outside the modal to trigger its closure, if allowed.
    /// </summary>
    private async Task HandleOutsideClick()
    {
        try
        {
            ModalService.Close();
            Log.Debug("Modal closed via outside click.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling outside click to close modal.");
        }

        await Task.CompletedTask;
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
