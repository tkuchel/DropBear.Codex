#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Validations;

/// <summary>
///     A Blazor component for displaying validation errors with a collapsible UI.
///     Optimized for Blazor Server with proper thread safety and state management.
/// </summary>
public sealed partial class DropBearValidationErrorsComponent : DropBearComponentBase
{
    #region Constructor

    public DropBearValidationErrorsComponent()
    {
        // Create a unique component ID.
        _componentId = $"validation-errors-{ComponentId}";
    }

    #endregion

    #region Cleanup

    /// <inheritdoc />
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (_jsModule != null)
            {
                await _stateSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                try
                {
                    // Create a temporary cancellation token with a 5-second timeout.
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _jsModule.InvokeVoidAsync(
                        $"{JsModuleName}API.dispose",
                        cts.Token,
                        _componentId);
                    LogDebug("Validation container cleaned up: {Id}", _componentId);
                }
                finally
                {
                    _stateSemaphore.Release();
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to cleanup validation container", ex);
        }
        finally
        {
            try
            {
                _stateSemaphore.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed.
            }

            _jsModule = null;
            _isInitialized = false;
        }
    }

    #endregion

    #region Private Fields & Constants

    private const string JsModuleName = JsModuleNames.ValidationErrors;

    private readonly string _componentId;
    private readonly SemaphoreSlim _stateSemaphore = new(1, 1);
    private volatile bool _isInitialized;
    private volatile bool _isCollapsed;
    private IJSObjectReference? _jsModule;

    #endregion

    #region Parameters

    /// <summary>
    ///     The validation result to display.
    /// </summary>
    [Parameter]
    public ValidationResult? ValidationResult { get; set; }

    /// <summary>
    ///     Whether the component is initially collapsed.
    /// </summary>
    [Parameter]
    public bool InitialCollapsed { get; set; }

    /// <summary>
    ///     Optional additional CSS classes.
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    #endregion

    #region Private Properties

    /// <summary>
    ///     Determines if there are any validation errors.
    /// </summary>
    private bool HasErrors => ValidationResult?.HasErrors == true;

    /// <summary>
    ///     Gets or sets the collapsed state and triggers ARIA attribute updates when changed.
    /// </summary>
    private bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value)
            {
                return;
            }

            _isCollapsed = value;

            if (!IsDisposed)
            {
                // Fire off an update of ARIA attributes without awaiting.
                _ = UpdateAriaAttributesAsync();
            }
        }
    }

    #endregion

    #region Lifecycle Methods

    protected override void OnInitialized()
    {
        base.OnInitialized();
        IsCollapsed = InitialCollapsed;

        if (HasErrors)
        {
            LogDebug("Initialized with {Count} errors", ValidationResult!.Errors.Count);
        }
    }

    /// <inheritdoc />
    protected override async Task InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _stateSemaphore.WaitAsync(ComponentToken);

            // Load the JavaScript module.
            _jsModule = await GetJsModuleAsync(JsModuleName);

            // Create the validation container via JS interop.
            await _jsModule.InvokeVoidAsync(
                $"{JsModuleName}API.createValidationContainer",
                ComponentToken,
                _componentId);

            // Update ARIA attributes based on the initial collapsed state.
            await _jsModule.InvokeVoidAsync(
                $"{JsModuleName}API.updateAriaAttributes",
                ComponentToken,
                _componentId,
                _isCollapsed);

            _isInitialized = true;
            LogDebug("Validation container initialized: {Id}", _componentId);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize validation container", ex);
            throw;
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    #endregion

    #region UI Interaction Methods

    /// <summary>
    ///     Toggles the collapsed state of the validation errors UI.
    /// </summary>
    private async Task ToggleCollapseState()
    {
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _stateSemaphore.WaitAsync(ComponentToken);

            // Queue a state update that toggles the collapse state and updates ARIA attributes.
            await QueueStateHasChangedAsync(async () =>
            {
                IsCollapsed = !IsCollapsed;
                await UpdateAriaAttributesAsync();
            });

            LogDebug("Collapse state toggled: {State}", IsCollapsed);
        }
        catch (Exception ex)
        {
            LogError("Failed to toggle collapse state", ex);
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    /// <summary>
    ///     Updates ARIA attributes via JS interop to reflect the current collapse state.
    /// </summary>
    private async Task UpdateAriaAttributesAsync()
    {
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _stateSemaphore.WaitAsync(ComponentToken);

            // Ensure the JS module is available.
            if (_jsModule == null)
            {
                await InitializeComponentAsync();
            }

            await _jsModule!.InvokeVoidAsync(
                $"{JsModuleName}API.updateAriaAttributes",
                ComponentToken,
                _componentId,
                _isCollapsed);

            LogDebug("ARIA attributes updated: {Id}", _componentId);
        }
        catch (Exception ex)
        {
            LogError("Failed to update ARIA attributes", ex);
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    #endregion
}
