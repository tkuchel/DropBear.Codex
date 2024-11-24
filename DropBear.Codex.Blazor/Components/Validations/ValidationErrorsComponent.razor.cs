#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Validations;

/// <summary>
///     A Blazor component for displaying validation errors with collapsible UI.
/// </summary>
public sealed partial class ValidationErrorsComponent : DropBearComponentBase
{
    private readonly string _componentId;
    private bool _isCollapsed;

    public ValidationErrorsComponent()
    {
        _componentId = $"validation-errors-{ComponentId}";
    }

    /// <summary>
    ///     The validation result containing errors to display.
    /// </summary>
    [Parameter]
    public ValidationResult? ValidationResult { get; set; }

    /// <summary>
    ///     Initial collapsed state of the validation errors panel.
    /// </summary>
    [Parameter]
    public bool InitialCollapsed { get; set; }

    /// <summary>
    ///     CSS class to apply to the validation errors container.
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <summary>
    ///     Gets a value indicating whether there are validation errors to display.
    /// </summary>
    private bool HasErrors => ValidationResult?.HasErrors == true;

    /// <summary>
    ///     Gets the current collapse state.
    /// </summary>
    private bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed != value)
            {
                _isCollapsed = value;
                UpdateAriaAttributes();
            }
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        IsCollapsed = InitialCollapsed;

        if (HasErrors)
        {
            Logger.Debug("Validation component initialized with {Count} errors",
                ValidationResult!.Errors.Count);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InvokeAsync(async () =>
            {
                try
                {
                    await Task.Delay(50); // Give DOM time to settle
                    await UpdateAriaAttributes();
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Error during first render initialization");
                }
            });
        }
    }

    private async Task ToggleCollapseState()
    {
        IsCollapsed = !IsCollapsed;
        await UpdateAriaAttributes();
        Logger.Debug("Validation panel collapsed state: {State}", IsCollapsed);
    }

    private async Task UpdateAriaAttributes()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            // First initialize the container
            await SafeJsVoidInteropAsync(
                "DropBearValidationErrors.initialize",
                _componentId);

            // Then update the aria attributes
            await SafeJsVoidInteropAsync(
                "validationErrors.updateAriaAttributes",
                _componentId,
                IsCollapsed);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error updating ARIA attributes: {Error}", ex.Message);
            // Don't throw - allow graceful degradation
        }
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            await SafeJsVoidInteropAsync(
                "DropBearValidationErrors.dispose",
                _componentId);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error during validation errors cleanup: {ComponentId}", _componentId);
        }
    }
}
