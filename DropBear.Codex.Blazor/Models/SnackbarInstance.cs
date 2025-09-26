#region

using System.ComponentModel.DataAnnotations;
using DropBear.Codex.Blazor.Enums;

#endregion

namespace DropBear.Codex.Blazor.Models;

/// <summary>
///     Represents a snackbar notification instance with all necessary display and behavior properties.
/// </summary>
public sealed record SnackbarInstance
{
    /// <summary>
    ///     Unique identifier for the snackbar instance.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     Optional title text displayed prominently.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///     Main message content (supports HTML markup).
    /// </summary>
    [Required]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    ///     Visual type/severity of the notification.
    /// </summary>
    public SnackbarType Type { get; init; } = SnackbarType.Information;

    /// <summary>
    ///     Duration in milliseconds before auto-close (0 = no auto-close).
    /// </summary>
    [Range(0, int.MaxValue)]
    public int Duration { get; init; } = 5000;

    /// <summary>
    ///     Delay in milliseconds before showing the snackbar.
    /// </summary>
    [Range(0, 10000)]
    public int ShowDelay { get; init; } = 0;

    /// <summary>
    ///     Whether the snackbar requires manual dismissal (ignores Duration).
    /// </summary>
    public bool RequiresManualClose { get; init; }

    /// <summary>
    ///     Collection of action buttons for the snackbar.
    /// </summary>
    public IReadOnlyList<SnackbarAction>? Actions { get; init; }

    /// <summary>
    ///     Timestamp when the snackbar was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Additional CSS classes to apply to the snackbar.
    /// </summary>
    public string? CssClass { get; init; }

    /// <summary>
    ///     Custom data for extensibility.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
