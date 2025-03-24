#region

using System.Runtime.CompilerServices;
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

    /// <summary>
    ///     Initializes a new instance of the <see cref="DropBearValidationErrorsComponent" /> class.
    /// </summary>
    public DropBearValidationErrorsComponent()
    {
        // Create a unique component ID.
        _componentId = $"validation-errors-{ComponentId}";
    }

    #endregion

    #region Cleanup

    /// <summary>
    ///     Cleans up JavaScript resources when the component is disposed.
    /// </summary>
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
    private bool _isCollapsed;
    private IJSObjectReference? _jsModule;

    // Backing fields for parameters
    private ValidationResult? _validationResult;
    private bool _initialCollapsed;
    private string? _cssClass;

    // Flag to track if component should render
    private bool _shouldRender = true;

    #endregion

    #region Parameters

    /// <summary>
    ///     The validation result to display.
    /// </summary>
    [Parameter]
    public ValidationResult? ValidationResult
    {
        get => _validationResult;
        set
        {
            if (_validationResult != value)
            {
                _validationResult = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Whether the component is initially collapsed.
    /// </summary>
    [Parameter]
    public bool InitialCollapsed
    {
        get => _initialCollapsed;
        set
        {
            if (_initialCollapsed != value)
            {
                _initialCollapsed = value;
                if (!_isInitialized)
                {
                    _isCollapsed = value;
                }

                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Optional additional CSS classes.
    /// </summary>
    [Parameter]
    public string? CssClass
    {
        get => _cssClass;
        set
        {
            if (_cssClass != value)
            {
                _cssClass = value;
                _shouldRender = true;
            }
        }
    }

    #endregion

    #region Private Properties

    /// <summary>
    ///     Determines if there are any validation errors.
    /// </summary>
    private bool HasErrors => _validationResult?.HasErrors == true;

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
            _shouldRender = true;

            if (!IsDisposed)
            {
                // Fire off an update of ARIA attributes without awaiting.
                _ = UpdateAriaAttributesAsync();
            }
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Controls whether the component should render, optimizing for performance.
    /// </summary>
    /// <returns>True if the component should render, false otherwise.</returns>
    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Initializes the component with the initial collapsed state.
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _isCollapsed = _initialCollapsed;

        if (HasErrors)
        {
            LogDebug("Initialized with {Count} errors", _validationResult!.Errors.Count);
        }
    }

    /// <summary>
    ///     Initializes JavaScript resources when the component is first rendered.
    /// </summary>
    protected override async ValueTask InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _stateSemaphore.WaitAsync(ComponentToken);

            // Load the JavaScript module.
            var jsModuleResult = await GetJsModuleAsync(JsModuleName);

            if (jsModuleResult.IsFailure)
            {
                LogError("Failed to load JS module: {Module}", jsModuleResult.Exception);
                return;
            }

            _jsModule = jsModuleResult.Value;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
