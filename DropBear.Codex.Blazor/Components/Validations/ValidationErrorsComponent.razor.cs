#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Blazor.Components.Validations;

/// <summary>
///     A Blazor component for displaying validation errors.
/// </summary>
public sealed partial class ValidationErrorsComponent : DropBearComponentBase
{
    [Inject] private ILogger<ValidationErrorsComponent> Logger { get; set; } = default!;

    private bool IsCollapsed { get; set; }

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
    ///     Gets a value indicating whether there are validation errors to display.
    /// </summary>
    private bool HasErrors => ValidationResult?.IsValid == false && ValidationResult.Errors.Any();

    protected override void OnInitialized()
    {
        base.OnInitialized();
        IsCollapsed = InitialCollapsed;

        if (HasErrors)
        {
            Logger.LogDebug("ValidationErrorsComponent initialized with {ErrorCount} errors.",
                ValidationResult!.Errors.Count);
        }
        else
        {
            Logger.LogDebug("ValidationErrorsComponent initialized with no errors.");
        }
    }

    /// <summary>
    ///     Toggles the collapse state of the validation errors panel.
    /// </summary>
    private void ToggleCollapseState()
    {
        IsCollapsed = !IsCollapsed;
        Logger.LogDebug("ValidationErrorsComponent collapsed state toggled to {IsCollapsed}.", IsCollapsed);
    }
}
