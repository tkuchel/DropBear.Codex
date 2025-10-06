using DropBear.Codex.Blazor.Enums;

namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
///     Configuration options for the snackbar service.
/// </summary>
public sealed class SnackbarOptions
{
    /// <summary>
    ///     Default duration for information snackbars in milliseconds.
    /// </summary>
    public int DefaultInformationDuration { get; set; } = 5000;

    /// <summary>
    ///     Default duration for success snackbars in milliseconds.
    /// </summary>
    public int DefaultSuccessDuration { get; set; } = 4000;

    /// <summary>
    ///     Default duration for warning snackbars in milliseconds.
    /// </summary>
    public int DefaultWarningDuration { get; set; } = 7000;

    /// <summary>
    ///     Default duration for error snackbars in milliseconds (0 = manual close).
    /// </summary>
    public int DefaultErrorDuration { get; set; } = 0;

    /// <summary>
    ///     Maximum number of concurrent snackbars to display.
    /// </summary>
    public int MaxConcurrentSnackbars { get; set; } = 5;

    /// <summary>
    ///     Default position for the snackbar container.
    /// </summary>
    public SnackbarPosition DefaultPosition { get; set; } = SnackbarPosition.BottomRight;

    /// <summary>
    ///     Whether to show newest snackbars first.
    /// </summary>
    public bool ShowNewestFirst { get; set; } = true;

    /// <summary>
    ///     Global CSS class to apply to all snackbars.
    /// </summary>
    public string? GlobalCssClass { get; set; }

    /// <summary>
    ///     Whether to enable debug logging.
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;
}
