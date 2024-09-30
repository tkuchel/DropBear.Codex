#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Validations;

/// <summary>
///     A Blazor component for displaying validation errors.
/// </summary>
public sealed partial class ValidationErrorsComponent : DropBearComponentBase
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ValidationErrorsComponent>();
    private bool _isCollapsed;

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

    protected override void OnInitialized()
    {
        _isCollapsed = InitialCollapsed;

        if (ValidationResult is not null && ValidationResult.Errors.Any())
        {
            Logger.Debug("ValidationErrorsComponent initialized with {ErrorCount} errors.", ValidationResult.Errors.Count);
        }
        else
        {
            Logger.Debug("ValidationErrorsComponent initialized with no errors.");
        }
    }

    /// <summary>
    ///     Toggles the collapse state of the validation errors panel.
    /// </summary>
    private void ToggleCollapseState()
    {
        _isCollapsed = !_isCollapsed;
        Logger.Debug("ValidationErrorsComponent collapsed state toggled to {IsCollapsed}.", _isCollapsed);
    }

    /// <summary>
    ///     Returns a CSS class based on whether the panel is collapsed.
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private string GetPanelCssClass() => _isCollapsed ? "validation-errors-collapsed" : "validation-errors-expanded";

    /// <summary>
    ///     Gets a CSS class for the validation message list.
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private string GetListCssClass() => _isCollapsed ? "hidden" : "visible";
}
