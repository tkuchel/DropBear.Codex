using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DropBear.Codex.Blazor.Components.Validations;

/// <summary>
/// A Blazor component for displaying validation errors with a collapsible UI,
/// refactored to match the "FileUploader" style JS interop pattern.
/// </summary>
public sealed partial class DropBearValidationErrorsComponent : DropBearComponentBase
{
    private IJSObjectReference? _jsModule;
    private const string JsModuleName = JsModuleNames.ValidationErrors;
    private bool _isCollapsed;

    // We generate a unique ID from the base class’s ComponentId
    private readonly string _componentId;

    /// <summary>
    /// Creates a new validation errors component and initializes its ID.
    /// </summary>
    public DropBearValidationErrorsComponent()
    {
        _componentId = $"validation-errors-{ComponentId}";
    }

    #region Parameters

    /// <summary>
    /// The validation result containing errors to display.
    /// </summary>
    [Parameter]
    public ValidationResult? ValidationResult { get; set; }

    /// <summary>
    /// If true, the errors panel is initially collapsed.
    /// </summary>
    [Parameter]
    public bool InitialCollapsed { get; set; }

    /// <summary>
    /// Additional CSS classes for the validation errors container.
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// Indicates whether there are validation errors to display.
    /// </summary>
    private bool HasErrors => ValidationResult?.HasErrors == true;

    /// <summary>
    /// Gets or sets the current collapse state of the panel.
    /// Changing this value calls <see cref="UpdateAriaAttributes"/> if the component is not disposed.
    /// </summary>
    private bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed == value) return;
            _isCollapsed = value;
            // Update ARIA attributes if we’re still alive
            if (!IsDisposed)
            {
                _ = UpdateAriaAttributes();
            }
        }
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        IsCollapsed = InitialCollapsed;

        if (HasErrors)
        {
            LogDebug("Validation component initialized with {Count} errors", ValidationResult!.Errors.Count);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// On first render, load the JS module and create the validation container
    /// in a manner similar to DropBearFileUploader's approach.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (!firstRender || IsDisposed)
            return;

        try
        {
            // 1) Load/cache the module
            _jsModule = await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);

            // 2) Create the container in JS
            await _jsModule.InvokeVoidAsync(
                $"{JsModuleName}API.createValidationContainer",
                _componentId
            );

            // 3) Now update ARIA attributes for the initial collapse state
            await _jsModule.InvokeVoidAsync(
                $"{JsModuleName}API.updateAriaAttributes",
                _componentId,
                IsCollapsed
            );

            LogDebug("Validation errors JS initialized: {Id}", _componentId);
        }
        catch (Exception ex)
        {
            LogWarning("Error during first render initialization for {Id}: {Message}", ex, _componentId, ex.Message);
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Toggles the collapse state and logs a debug message.
    /// </summary>
    private async Task ToggleCollapseState()
    {
        IsCollapsed = !IsCollapsed;
        LogDebug("Validation panel collapsed state: {State}", IsCollapsed);

        // Optionally call UpdateAriaAttributes again explicitly,
        // though the IsCollapsed setter already does this.
        // await UpdateAriaAttributes();
    }

    /// <summary>
    /// Updates ARIA attributes via JS interop to maintain accessibility state.
    /// Mimics the "FileUploader" style: we get (or reuse) the module and call the relevant function.
    /// </summary>
    private async Task UpdateAriaAttributes()
    {
        if (IsDisposed)
            return;

        try
        {
            // If we haven't loaded or lost the module reference, reacquire it
            _jsModule ??= await GetJsModuleAsync(JsModuleName).ConfigureAwait(false);

            // Then update the ARIA attributes
            await _jsModule.InvokeVoidAsync(
                $"{JsModuleName}API.updateAriaAttributes",
                _componentId,
                IsCollapsed
            );
        }
        catch (Exception ex)
        {
            LogWarning("Error updating ARIA attributes for {Id}: {Message}", ex, _componentId, ex.Message);
        }
    }

    #endregion

    #region Disposal

    /// <inheritdoc />
    /// <remarks>
    /// Called by the base class to cleanup JS resources. We call 'DropBearValidationErrors.dispose'
    /// to remove the manager for our unique container ID.
    /// </remarks>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        if (_jsModule is null)
            return;

        try
        {
            await _jsModule.InvokeVoidAsync($"{JsModuleName}API.dispose", _componentId);
            LogDebug("Validation errors JS disposed: {Id}", _componentId);
        }
        catch (JSDisconnectedException)
        {
            LogWarning("Cleanup skipped: JS runtime disconnected.");
        }
        catch (TaskCanceledException)
        {
            LogWarning("Cleanup skipped: Operation cancelled.");
        }
        catch (Exception ex)
        {
            LogWarning("Error disposing validation errors JS for {Id}: {Message}", ex, _componentId, ex.Message);
        }
        finally
        {
            _jsModule = null;
        }
    }

    #endregion
}
