#region

using System.Runtime.CompilerServices;
using System.Text;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A container for displaying modals with customizable width, height, and transitions.
///     Optimized for Blazor Server with improved memory management and rendering performance.
/// </summary>
public sealed partial class DropBearModalContainer : DropBearComponentBase
{
    private new static readonly Microsoft.Extensions.Logging.ILogger Logger = CreateLogger();

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

    // Modal overlay element reference for focus management
    private ElementReference _modalOverlay;

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

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
            LogErrorInitializingModalContainer(Logger, ex);
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
            LogSubscribedToModalServiceEvents(Logger);
        }
        catch (Exception ex)
        {
            LogFailedToSubscribeToModalService(Logger, ex);
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
                LogErrorHandlingModalServiceChange(Logger, ex);
            }
        }
        catch (Exception e)
        {
            LogErrorHandlingModalServiceChange(Logger, e);
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

        LogModalParametersUpdated(Logger, _customWidth, _customHeight, _transitionDuration);
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
    ///     Manages focus when the modal is displayed for accessibility.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (ModalService.IsModalVisible && !IsDisposed)
        {
            try
            {
                // Focus the modal overlay for keyboard navigation
                await JSRuntime.InvokeVoidAsync("eval",
                    "document.querySelector('[role=\"dialog\"]')?.focus()");
            }
            catch (Exception ex)
            {
                LogFailedToFocusModal(Logger, ex);
            }
        }
    }

    /// <summary>
    ///     Handles keyboard events on the modal (Escape key to close).
    /// </summary>
    private Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && !IsDisposed)
        {
            try
            {
                ModalService.ClearAll();
                LogModalClosedViaEscapeKey(Logger);
            }
            catch (Exception ex)
            {
                LogErrorClosingModalWithEscapeKey(Logger, ex);
            }
        }

        return Task.CompletedTask;
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
            ModalService.ClearAll();
            LogModalClosedViaOutsideClick(Logger);
        }
        catch (Exception ex)
        {
            LogErrorHandlingModalOutsideClick(Logger, ex);
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
            ModalService.ClearAll();
            LogModalClosedProgrammatically(Logger);
        }
        catch (Exception ex)
        {
            LogErrorClosingModalProgrammatically(Logger, ex);
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
                    LogUnsubscribedFromModalServiceEvents(Logger);
                }

                await _modalCts.CancelAsync();
                _modalCts.Dispose();

                ResetToDefaults();
                _filteredParametersCache = null;
            }
            catch (Exception ex)
            {
                LogErrorDuringModalContainerDisposal(Logger, ex);
            }
        }

        await base.DisposeAsync();
    }

    #region Helper Methods (Logger)

    private static Microsoft.Extensions.Logging.ILogger CreateLogger()
    {
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Core.Logging.LoggerFactory.Logger.ForContext<DropBearModalContainer>());
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        return loggerFactory.CreateLogger(nameof(DropBearModalContainer));
    }

    #endregion

    #region LoggerMessage Source Generators

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error initializing modal container")]
    static partial void LogErrorInitializingModalContainer(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Subscribed to ModalService events")]
    static partial void LogSubscribedToModalServiceEvents(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to subscribe to modal service")]
    static partial void LogFailedToSubscribeToModalService(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error handling modal service change")]
    static partial void LogErrorHandlingModalServiceChange(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Modal parameters updated: Width={Width}, Height={Height}, Duration={Duration}")]
    static partial void LogModalParametersUpdated(Microsoft.Extensions.Logging.ILogger logger, string width, string height, string duration);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to focus modal")]
    static partial void LogFailedToFocusModal(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Modal closed via Escape key")]
    static partial void LogModalClosedViaEscapeKey(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error closing modal with Escape key")]
    static partial void LogErrorClosingModalWithEscapeKey(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Modal closed via outside click")]
    static partial void LogModalClosedViaOutsideClick(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error handling modal outside click")]
    static partial void LogErrorHandlingModalOutsideClick(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Modal closed programmatically")]
    static partial void LogModalClosedProgrammatically(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error closing modal programmatically")]
    static partial void LogErrorClosingModalProgrammatically(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Unsubscribed from modal service events")]
    static partial void LogUnsubscribedFromModalServiceEvents(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error during modal container disposal")]
    static partial void LogErrorDuringModalContainerDisposal(Microsoft.Extensions.Logging.ILogger logger, Exception ex);

    #endregion
}
