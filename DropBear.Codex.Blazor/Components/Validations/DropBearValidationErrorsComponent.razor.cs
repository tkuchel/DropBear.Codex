#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Results.Validations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

#endregion

namespace DropBear.Codex.Blazor.Components.Validations;

/// <summary>
///     Modern validation errors component optimized for .NET 9+ and Blazor Server.
///     Features responsive design, smooth animations, and enhanced accessibility.
/// </summary>
public sealed partial class DropBearValidationErrorsComponent : DropBearComponentBase
{
    #region Constants

    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan _AutoCollapseDelay = TimeSpan.FromSeconds(5);

    #endregion

    #region Fields

    private readonly CancellationTokenSource _componentCts = new();

    private ValidationResult? _previousValidationResult;
    private bool _isCollapsed = true; // Start collapsed by default
    private DateTime _lastErrorTime = DateTime.MinValue;
    private int _previousErrorCount;

    // Cached computations for performance
    private string? _cachedComponentId;
    private bool _hasNewErrors;

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets the validation result to display.
    /// </summary>
    [Parameter]
    public ValidationResult? ValidationResult { get; set; }

    /// <summary>
    ///     Gets or sets whether the component should start in a collapsed state.
    /// </summary>
    [Parameter]
    public bool InitiallyCollapsed { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to automatically collapse after a delay.
    /// </summary>
    [Parameter]
    public bool AutoCollapse { get; set; } = true;

    /// <summary>
    ///     Gets or sets the delay before auto-collapsing.
    /// </summary>
    [Parameter]
    public TimeSpan AutoCollapseDelay { get; set; } = _AutoCollapseDelay;

    /// <summary>
    ///     Gets or sets whether to show error count in the header.
    /// </summary>
    [Parameter]
    public bool ShowErrorCount { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to automatically expand when new errors are added.
    /// </summary>
    [Parameter]
    public bool ExpandOnNewErrors { get; set; } = true;

    /// <summary>
    ///     Gets or sets the maximum height for the error list (in pixels).
    /// </summary>
    [Parameter]
    public int MaxHeight { get; set; } = 300;

    /// <summary>
    ///     Gets or sets additional CSS classes.
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <summary>
    ///     Event callback fired when the collapse state changes.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnCollapseStateChanged { get; set; }

    /// <summary>
    ///     Event callback fired when an error is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<ValidationError> OnErrorClicked { get; set; }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets whether there are validation errors to display.
    /// </summary>
    public bool HasErrors => ValidationResult?.IsValid == false;

    /// <summary>
    ///     Gets the number of validation errors.
    /// </summary>
    public int ErrorCount => ValidationResult?.Errors.Count ?? 0;

    /// <summary>
    ///     Gets whether the component is currently collapsed.
    /// </summary>
    public bool IsCollapsed => _isCollapsed;

    /// <summary>
    ///     Gets whether there are new errors since last check.
    /// </summary>
    public bool HasNewErrors => _hasNewErrors;

    /// <summary>
    ///     Gets the unique component identifier.
    /// </summary>
    private string ComponentElementId => _cachedComponentId ??= $"validation-errors-{ComponentId}";

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Component initialization.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _isCollapsed = InitiallyCollapsed;
        _previousValidationResult = ValidationResult;
        _previousErrorCount = ErrorCount;
    }

    /// <summary>
    ///     Handles parameter changes with optimized change detection.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        var hasResultChanged = !ReferenceEquals(ValidationResult, _previousValidationResult);
        var hasErrorCountChanged = ErrorCount != _previousErrorCount;

        if (hasResultChanged || hasErrorCountChanged)
        {
            _hasNewErrors = HasErrors && ErrorCount > _previousErrorCount;

            // Auto-expand on new errors if configured
            if (_hasNewErrors && ExpandOnNewErrors && _isCollapsed)
            {
                _isCollapsed = false;
                _lastErrorTime = DateTime.UtcNow;
                await OnCollapseStateChanged.InvokeAsync(false);
            }

            _previousValidationResult = ValidationResult;
            _previousErrorCount = ErrorCount;
        }

        await base.OnParametersSetAsync();
    }

    /// <summary>
    ///     Optimized rendering control.
    /// </summary>
    protected override bool ShouldRender()
    {
        // Only render if we have meaningful changes
        return !IsDisposed && (
            !ReferenceEquals(ValidationResult, _previousValidationResult) ||
            ErrorCount != _previousErrorCount ||
            _hasNewErrors
        );
    }

    /// <summary>
    ///     Component disposal.
    /// </summary>
    protected override async ValueTask DisposeAsyncCore()
    {
        await _componentCts.CancelAsync();
        _componentCts.Dispose();

        await base.DisposeAsyncCore();
    }

    #endregion

    #region Public API Methods

    /// <summary>
    ///     Programmatically toggles the collapsed state.
    /// </summary>
    /// <param name="collapsed">Optional specific state to set. If null, toggles current state.</param>
    public async Task SetCollapsedStateAsync(bool? collapsed = null)
    {
        if (IsDisposed) return;

        var newState = collapsed ?? !_isCollapsed;

        if (newState != _isCollapsed)
        {
            _isCollapsed = newState;

            if (!_isCollapsed)
            {
                _lastErrorTime = DateTime.UtcNow;
            }

            await OnCollapseStateChanged.InvokeAsync(newState);
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    ///     Clears the new errors flag.
    /// </summary>
    public void ClearNewErrorsFlag()
    {
        _hasNewErrors = false;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Handles the header click to toggle collapsed state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task HandleHeaderClickAsync()
    {
        await SetCollapsedStateAsync();
    }

    /// <summary>
    ///     Handles keyboard navigation for the header.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task HandleHeaderKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " ")
        {
            await SetCollapsedStateAsync();
        }
    }

    /// <summary>
    ///     Handles error item clicks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task HandleErrorClickAsync(ValidationError error)
    {
        if (OnErrorClicked.HasDelegate)
        {
            await OnErrorClicked.InvokeAsync(error);
        }
    }

    /// <summary>
    ///     Gets the CSS classes for the main container.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetContainerClasses()
    {
        var classes = "validation-errors";

        if (_isCollapsed) classes += " validation-errors--collapsed";
        if (_hasNewErrors) classes += " validation-errors--new-errors";
        if (!string.IsNullOrEmpty(CssClass)) classes += $" {CssClass}";

        return classes;
    }

    /// <summary>
    ///     Gets the style for the error list container.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetListStyle()
    {
        return $"max-height: {MaxHeight}px;";
    }

    /// <summary>
    ///     Gets the error severity icon.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MarkupString GetSeverityIcon(ValidationError error)
    {
        // You can extend this to support different severity levels
        return new MarkupString("""
                                    <svg viewBox="0 0 20 20" fill="currentColor" class="error-icon">
                                        <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/>
                                    </svg>
                                """);
    }

    /// <summary>
    ///     Gets the expand/collapse chevron icon.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MarkupString GetChevronIcon()
    {
        return new MarkupString("""
                                    <svg viewBox="0 0 20 20" fill="currentColor" class="chevron-icon">
                                        <path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd"/>
                                    </svg>
                                """);
    }

    /// <summary>
    ///     Formats the parameter name for display.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatParameterName(string parameter)
    {
        if (string.IsNullOrEmpty(parameter)) return "Field";

        // Convert camelCase to Title Case
        return System.Text.RegularExpressions.Regex.Replace(parameter,
            @"(\B[A-Z])", " $1");
    }

    #endregion
}
