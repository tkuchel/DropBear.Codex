#region

using DropBear.Codex.Blazor.Components.Bases;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container for displaying modals with customizable width, height, and transitions.
/// </summary>
public sealed partial class DropBearModalContainer : DropBearComponentBase, IDisposable
{
    private readonly string _modalTransitionClass = "enter"; // Controls enter/leave animations
    private string _customHeight = "auto"; // Default height
    private string _customWidth = "auto"; // Default width
    private bool _isSubscribed;
    private string _transitionDuration = "0.3s"; // Default animation duration

    /// <summary>
    ///     Ensures we unsubscribe from the modal service event on disposal.
    /// </summary>
    public void Dispose()
    {
        UnsubscribeFromModalService();
        // GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        SubscribeToModalService();
    }

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
    ///     Reads and applies custom parameters (width, height, transition duration) from <see cref="ModalService" />.
    /// </summary>
    private void SetCustomParameters()
    {
        if (ModalService.CurrentParameters != null)
        {
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
            "Modal container initialized with custom dimensions: Width={Width}, Height={Height}, Duration={Duration}",
            _customWidth, _customHeight, _transitionDuration);
    }

    /// <summary>
    ///     Handles a click outside the modal content to close the modal if allowed.
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
    ///     Allows manual triggering of modal closure from external code.
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
