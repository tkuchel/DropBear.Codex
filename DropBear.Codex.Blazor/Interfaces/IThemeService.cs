#region

using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Blazor.Errors;

#endregion

namespace DropBear.Codex.Blazor.Interfaces;

/// <summary>
///     Service for managing application theme (light/dark mode) with system preference detection,
///     smooth transitions, and persistent storage.
/// </summary>
public interface IThemeService : IAsyncDisposable
{
    /// <summary>
    ///     Event raised when the theme changes.
    /// </summary>
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    ///     Gets a value indicating whether the service has been initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    ///     Initializes the theme service and detects system preferences.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A result containing the current theme information.</returns>
    ValueTask<Result<ThemeInfo, JsInteropError>> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sets the theme preference (light, dark, or auto).
    /// </summary>
    /// <param name="theme">The theme to set.</param>
    /// <param name="animated">Whether to animate the transition.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    ValueTask<Result<bool, JsInteropError>> SetThemeAsync(
        Theme theme,
        bool animated = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Toggles between light and dark themes.
    /// </summary>
    /// <param name="animated">Whether to animate the transition.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A result containing the new theme.</returns>
    ValueTask<Result<Theme, JsInteropError>> ToggleThemeAsync(
        bool animated = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the current theme information.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A result containing the current theme information.</returns>
    ValueTask<Result<ThemeInfo, JsInteropError>> GetThemeInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Applies a theme override to a specific element.
    /// </summary>
    /// <param name="elementReference">Reference to the element.</param>
    /// <param name="theme">The theme to apply.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    ValueTask<Result<bool, JsInteropError>> ApplyThemeToElementAsync(
        Microsoft.AspNetCore.Components.ElementReference elementReference,
        Theme theme,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes theme override from a specific element.
    /// </summary>
    /// <param name="elementReference">Reference to the element.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    ValueTask<Result<bool, JsInteropError>> RemoveThemeFromElementAsync(
        Microsoft.AspNetCore.Components.ElementReference elementReference,
        CancellationToken cancellationToken = default);
}
