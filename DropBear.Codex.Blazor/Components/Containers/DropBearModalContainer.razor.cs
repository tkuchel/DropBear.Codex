#region

using DropBear.Codex.Blazor.Components.Bases;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container for displaying modals with customizable width, height, and transitions.
/// </summary>
public sealed partial class DropBearModalContainer : DropBearComponentBase
{
    private const string DEFAULT_TRANSITION_DURATION = "0.3s";
    private const string DEFAULT_DIMENSION = "auto";
    private const string ENTER_TRANSITION_CLASS = "enter";

    /// <summary>
    ///     The keys we want to remove from the child parameters dictionary
    ///     (because these belong to the container, not the child).
    /// </summary>
    private static readonly string[] ContainerKeys = { "CustomWidth", "CustomHeight", "TransitionDuration" };

    public IDictionary<string, object>? ChildParameters => FilterOutModalParams(ModalService.CurrentParameters);

    private readonly string _modalTransitionClass = ENTER_TRANSITION_CLASS;
    private string _customHeight = DEFAULT_DIMENSION;
    private string _customWidth = DEFAULT_DIMENSION;
    private bool _isSubscribed;
    private string _transitionDuration = DEFAULT_TRANSITION_DURATION;

    /// <summary>
    ///     CSS style string for the modal dimensions and transitions.
    /// </summary>
    private string ModalStyle => BuildModalStyle();

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        try
        {
            base.OnInitialized();
            SubscribeToModalService();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error initializing modal container");
            throw;
        }
    }

    /// <summary>
    ///     Subscribes to the modal service events and initializes custom parameters.
    /// </summary>
    private void SubscribeToModalService()
    {
        if (_isSubscribed || IsDisposed)
        {
            return;
        }

        try
        {
            ModalService.OnChange += HandleModalServiceChange;
            _isSubscribed = true;
            UpdateCustomParameters();
            Logger.Debug("Subscribed to ModalService events");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to subscribe to modal service");
            throw;
        }
    }

    /// <summary>
    ///     Handles changes from the modal service.
    /// </summary>
    private async void HandleModalServiceChange()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            UpdateCustomParameters();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling modal service change");
        }
    }

    /// <summary>
    ///     Updates modal parameters from the service configuration.
    /// </summary>
    private void UpdateCustomParameters()
    {
        if (ModalService.CurrentParameters is null)
        {
            ResetToDefaults();
            return;
        }

        _customWidth = GetParameterValue("CustomWidth", DEFAULT_DIMENSION);
        _customHeight = GetParameterValue("CustomHeight", DEFAULT_DIMENSION);
        _transitionDuration = GetParameterValue("TransitionDuration", DEFAULT_TRANSITION_DURATION);

        Logger.Debug("Modal parameters updated: Width={Width}, Height={Height}, Duration={Duration}",
            _customWidth, _customHeight, _transitionDuration);
    }

    /// <summary>
    ///     Gets a parameter value from the modal service, with fallback.
    /// </summary>
    private string GetParameterValue(string key, string defaultValue)
    {
        return ModalService.CurrentParameters?.TryGetValue(key, out var value) == true
            ? value.ToString() ?? defaultValue
            : defaultValue;
    }

    /// <summary>
    ///     Creates a copy of <paramref name="originalParams" /> with modal-specific
    ///     keys removed, so they won't be passed to child components.
    /// </summary>
    private IDictionary<string, object>? FilterOutModalParams(IDictionary<string, object>? originalParams)
    {
        if (originalParams is null)
        {
            return null;
        }

        var filtered = new Dictionary<string, object>(originalParams);
        foreach (var key in ContainerKeys)
        {
            // Remove the key if present
            filtered.Remove(key);
        }

        return filtered;
    }

    /// <summary>
    ///     Resets modal parameters to their default values.
    /// </summary>
    private void ResetToDefaults()
    {
        _customWidth = DEFAULT_DIMENSION;
        _customHeight = DEFAULT_DIMENSION;
        _transitionDuration = DEFAULT_TRANSITION_DURATION;
    }

    /// <summary>
    ///     Builds the CSS style string for the modal.
    /// </summary>
    private string BuildModalStyle()
    {
        return $"width: {_customWidth}; height: {_customHeight}; transition-duration: {_transitionDuration};";
    }

    /// <summary>
    ///     Handles clicks outside the modal content.
    /// </summary>
    private Task HandleOutsideClick()
    {
        try
        {
            ModalService.Close();
            Logger.Debug("Modal closed via outside click");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling modal outside click");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Allows programmatic modal closure.
    /// </summary>
    public void Close()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            ModalService.Close();
            Logger.Debug("Modal closed programmatically");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error closing modal programmatically");
        }
    }

    /// <summary>
    ///     Disposes of the component, cleaning up any resources.
    ///     This method is called by the Blazor framework when the component is removed from the UI.
    /// </summary>
    public override ValueTask DisposeAsync()
    {
        if (!IsDisposed)
        {
            if (_isSubscribed)
            {
                ModalService.OnChange -= HandleModalServiceChange;
                _isSubscribed = false;
                Logger.Debug("Unsubscribed from modal service events");
            }

            ResetToDefaults();
        }

        _ = base.DisposeAsync();

        return ValueTask.CompletedTask;
    }
}
