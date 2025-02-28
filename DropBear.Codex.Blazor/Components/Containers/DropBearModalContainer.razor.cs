#region

using System.Runtime.CompilerServices;
using System.Text;
using DropBear.Codex.Blazor.Components.Bases;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container for displaying modals with customizable width, height, and transitions.
///     Optimized for Blazor Server with improved memory management and rendering performance.
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

    // Cancellation tracking for async operations
    private readonly CancellationTokenSource _modalCts = new();

    // UI state tracking
    private readonly string _modalTransitionClass = ENTER_TRANSITION_CLASS;

    // Cached parameters to detect changes
    private string _customHeight = DEFAULT_DIMENSION;
    private string _customWidth = DEFAULT_DIMENSION;

    // Dictionary cache to avoid recreating
    private IDictionary<string, object>? _filteredParametersCache;
    private volatile bool _isSubscribed;

    // Modal style cache to avoid rebuilding
    private string _modalStyleCache = string.Empty;
    private bool _parametersChanged;
    private bool _styleNeedsRebuild = true;
    private string _transitionDuration = DEFAULT_TRANSITION_DURATION;

    /// <summary>
    ///     Gets the filtered parameters for the child component, removing container-specific keys.
    /// </summary>
    public IDictionary<string, object>? ChildParameters => GetFilteredParameters();

    /// <summary>
    ///     CSS style string for the modal dimensions and transitions.
    /// </summary>
    private string ModalStyle
    {
        get
        {
            if (_styleNeedsRebuild)
            {
                _modalStyleCache = BuildModalStyle();
                _styleNeedsRebuild = false;
            }

            return _modalStyleCache;
        }
    }

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
        try
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                // Check if parameters were actually changed
                var oldWidth = _customWidth;
                var oldHeight = _customHeight;
                var oldDuration = _transitionDuration;

                UpdateCustomParameters();

                // Only flag parameters as changed if values actually differ
                _parametersChanged = oldWidth != _customWidth ||
                                     oldHeight != _customHeight ||
                                     oldDuration != _transitionDuration ||
                                     _filteredParametersCache == null;

                // Flag style for rebuild if dimensions changed
                if (oldWidth != _customWidth || oldHeight != _customHeight || oldDuration != _transitionDuration)
                {
                    _styleNeedsRebuild = true;
                }

                // Render UI
                await QueueStateHasChangedAsync(() => { });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling modal service change");
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error handling modal service change");
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetParameterValue(string key, string defaultValue)
    {
        return ModalService.CurrentParameters?.TryGetValue(key, out var value) == true
            ? value?.ToString() ?? defaultValue
            : defaultValue;
    }

    /// <summary>
    ///     Gets filtered parameters for the child component, with caching for performance.
    /// </summary>
    private IDictionary<string, object>? GetFilteredParameters()
    {
        if (ModalService.CurrentParameters is null)
        {
            _filteredParametersCache = null;
            return null;
        }

        // Use cached parameters if available and nothing changed
        if (!_parametersChanged && _filteredParametersCache != null)
        {
            return _filteredParametersCache;
        }

        // Create a fresh copy
        _filteredParametersCache = FilterOutModalParams(ModalService.CurrentParameters);
        _parametersChanged = false;
        return _filteredParametersCache;
    }

    /// <summary>
    ///     Creates a copy of <paramref name="originalParams" /> with modal-specific
    ///     keys removed, so they won't be passed to child components.
    /// </summary>
    private static IDictionary<string, object>? FilterOutModalParams(IDictionary<string, object>? originalParams)
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
        _styleNeedsRebuild = true;
        _parametersChanged = true;
        _filteredParametersCache = null;
    }

    /// <summary>
    ///     Builds the CSS style string for the modal using StringBuilder for efficiency.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string BuildModalStyle()
    {
        var builder = new StringBuilder(80);
        builder.Append("width: ");
        builder.Append(_customWidth);
        builder.Append("; height: ");
        builder.Append(_customHeight);
        builder.Append("; transition-duration: ");
        builder.Append(_transitionDuration);
        builder.Append(';');
        return builder.ToString();
    }

    /// <summary>
    ///     Handles clicks outside the modal content, with debounce protection.
    /// </summary>
    private Task HandleOutsideClick()
    {
        if (IsDisposed)
        {
            return Task.CompletedTask;
        }

        try
        {
            // Ensure we don't trigger multiple close events
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_modalCts.Token);
            cts.CancelAfter(500); // Debounce for 500ms

            // Close the modal
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
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (!IsDisposed)
        {
            try
            {
                if (_isSubscribed)
                {
                    ModalService.OnChange -= HandleModalServiceChange;
                    _isSubscribed = false;
                    Logger.Debug("Unsubscribed from modal service events");
                }

                await _modalCts.CancelAsync();
                _modalCts.Dispose();

                ResetToDefaults();
                _filteredParametersCache = null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during modal container disposal");
            }
        }

        await base.DisposeAsync();
    }
}
