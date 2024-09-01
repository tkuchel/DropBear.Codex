#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Validations;

/// <summary>
///     A Blazor component for displaying validation errors.
/// </summary>
public sealed partial class ValidationErrorsComponent : DropBearComponentBase
{
    private bool _isCollapsed;

    /// <summary>
    ///     Gets or sets the validation result to be displayed.
    /// </summary>
    [Parameter]
    public ValidationResult? ValidationResult { get; set; }

    /// <summary>
    ///     Toggles the collapse state of the validation errors panel.
    /// </summary>
    private void ToggleCollapseState()
    {
        _isCollapsed = !_isCollapsed;
    }
}
