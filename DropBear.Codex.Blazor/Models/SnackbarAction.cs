using System.ComponentModel.DataAnnotations;

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents an action button within a snackbar.
/// </summary>
public sealed record SnackbarAction
{
    /// <summary>
    ///     Unique identifier for the action.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     Display text for the action button.
    /// </summary>
    [Required]
    public string Label { get; init; } = string.Empty;

    /// <summary>
    ///     Whether this is the primary action (different styling).
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    ///     Whether the action is currently disabled.
    /// </summary>
    public bool IsDisabled { get; init; }

    /// <summary>
    ///     Callback invoked when the action is clicked.
    /// </summary>
    public Func<Task>? OnClick { get; init; }

    /// <summary>
    ///     Optional icon name or CSS class for the action.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    ///     Additional CSS classes for the action button.
    /// </summary>
    public string? CssClass { get; init; }

    /// <summary>
    ///     Tooltip text for the action.
    /// </summary>
    public string? Tooltip { get; init; }
}
