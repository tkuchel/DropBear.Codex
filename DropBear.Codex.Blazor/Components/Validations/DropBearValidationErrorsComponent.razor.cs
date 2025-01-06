#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Validations;

/// <summary>
///     A Blazor component for displaying validation errors with a collapsible UI.
/// </summary>
public sealed partial class DropBearValidationErrorsComponent : DropBearComponentBase
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearValidationErrorsComponent>();
    private readonly string _componentId;
    private bool _isCollapsed;

    /// <summary>
    ///     Creates a new validation errors component and initializes its ID.
    /// </summary>
    public DropBearValidationErrorsComponent()
    {
        _componentId = $"validation-errors-{ComponentId}";
    }

    /// <summary>
    ///     The validation result containing errors to display.
    /// </summary>
    [Parameter]
    public ValidationResult? ValidationResult { get; set; }

    /// <summary>
    ///     If true, the errors panel is initially collapsed.
    /// </summary>
    [Parameter]
    public bool InitialCollapsed { get; set; }

    /// <summary>
    ///     Additional CSS classes for the validation errors container.
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <summary>
    ///     Indicates whether there are validation errors to display.
    /// </summary>
    private bool HasErrors => ValidationResult?.HasErrors == true;

    /// <summary>
    ///     Gets or sets the current collapse state of the panel.
    /// </summary>
    private bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed != value)
            {
                _isCollapsed = value;
                _ = UpdateAriaAttributes();
            }
        }
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            try
            {
                // Slight delay to ensure DOM availability
                await Task.Delay(50);
                await UpdateAriaAttributes();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error during first render initialization");
            }
        }
    }

    /// <summary>
    ///     Toggles the collapse state and updates ARIA attributes.
    /// </summary>
    private async Task ToggleCollapseState()
    {
        IsCollapsed = !IsCollapsed;
        await UpdateAriaAttributes();
        Logger.Debug("Validation panel collapsed state: {State}", IsCollapsed);
    }

    /// <summary>
    ///     Updates ARIA attributes via JS interop to maintain accessibility state.
    /// </summary>
    private async Task UpdateAriaAttributes()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            // Initialize the container first
            await SafeJsVoidInteropAsync(
                "DropBearValidationErrors.initialize",
                _componentId);

            // Then update the ARIA attributes
            await SafeJsVoidInteropAsync(
                "validationErrors.updateAriaAttributes",
                _componentId,
                IsCollapsed);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error updating ARIA attributes: {Message}", ex.Message);
        }
    }

    /// <inheritdoc />
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            await SafeJsVoidInteropAsync("DropBearValidationErrors.dispose", _componentId);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error during validation errors cleanup: {ComponentId}", _componentId);
        }
    }
}
