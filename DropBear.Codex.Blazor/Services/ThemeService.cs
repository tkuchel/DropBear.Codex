#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Results.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Service for managing application theme (light/dark mode) with system preference detection,
///     smooth transitions, and persistent storage.
/// </summary>
public sealed class ThemeService : IThemeService
{
    #region Fields and Constants

    private const string ModulePath = "./js/DropBearThemeManager.module.js";
    private const int InitializationTimeoutSeconds = 10;

    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private IJSObjectReference? _moduleReference;
    private bool _isInitialized;
    private int _isDisposed;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    /// <param name="jsRuntime">The JavaScript runtime.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException">Thrown if either parameter is null.</exception>
    public ThemeService(IJSRuntime jsRuntime, ILogger logger)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Events

    /// <inheritdoc />
#pragma warning disable CS0067 // Event is part of IThemeService interface but not yet implemented
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
#pragma warning restore CS0067

    #endregion

    #region Properties

    /// <inheritdoc />
    public bool IsInitialized => _isInitialized;

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public async ValueTask<Result<ThemeInfo, JsInteropError>> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_isInitialized)
        {
            _logger.Debug("ThemeService already initialized");
            return await GetThemeInfoAsync(cancellationToken).ConfigureAwait(false);
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_isInitialized)
            {
                return await GetThemeInfoAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.Information("Initializing ThemeService");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(InitializationTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                // Import the JavaScript module
                _moduleReference = await _jsRuntime
                    .InvokeAsync<IJSObjectReference>("import", linkedCts.Token, ModulePath)
                    .ConfigureAwait(false);

                // Call initialize function
                var themeInfoDict = await _moduleReference
                    .InvokeAsync<Dictionary<string, object>>("initialize", linkedCts.Token)
                    .ConfigureAwait(false);

                var themeInfo = ThemeInfo.FromDictionary(themeInfoDict);
                _isInitialized = true;

                _logger.Information(
                    "ThemeService initialized successfully. Current theme: {Theme}, User preference: {Preference}",
                    themeInfo.Current,
                    themeInfo.UserPreference);

                return Result<ThemeInfo, JsInteropError>.Success(themeInfo);
            }
            catch (JSException ex)
            {
                _logger.Error(ex, "JavaScript error during theme service initialization");
                return Result<ThemeInfo, JsInteropError>.Failure(
                    JsInteropError.InvocationFailed("initialize", ex.Message),
                    ex);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.Error("Theme service initialization timed out after {Timeout}s", InitializationTimeoutSeconds);
                return Result<ThemeInfo, JsInteropError>.Failure(
                    JsInteropError.Timeout("DropBearThemeManager", InitializationTimeoutSeconds));
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<bool, JsInteropError>> SetThemeAsync(
        Theme theme,
        bool animated = true,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var ensureResult = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!ensureResult.IsSuccess)
        {
            return Result<bool, JsInteropError>.Failure(ensureResult.Error!);
        }

        try
        {
            var themeString = theme.ToString().ToLowerInvariant();
            var success = await _moduleReference!
                .InvokeAsync<bool>("setTheme", cancellationToken, themeString, animated)
                .ConfigureAwait(false);

            if (success)
            {
                _logger.Debug("Theme set to {Theme} (animated: {Animated})", theme, animated);
            }
            else
            {
                _logger.Warning("Failed to set theme to {Theme}", theme);
            }

            return Result<bool, JsInteropError>.Success(success);
        }
        catch (JSException ex)
        {
            _logger.Error(ex, "JavaScript error setting theme to {Theme}", theme);
            return Result<bool, JsInteropError>.Failure(
                JsInteropError.InvocationFailed("setTheme", ex.Message),
                ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<Theme, JsInteropError>> ToggleThemeAsync(
        bool animated = true,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var ensureResult = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!ensureResult.IsSuccess)
        {
            return Result<Theme, JsInteropError>.Failure(ensureResult.Error!);
        }

        try
        {
            var newThemeString = await _moduleReference!
                .InvokeAsync<string>("toggleTheme", cancellationToken, animated)
                .ConfigureAwait(false);

            var newTheme = ParseTheme(newThemeString);
            _logger.Debug("Theme toggled to {Theme} (animated: {Animated})", newTheme, animated);

            return Result<Theme, JsInteropError>.Success(newTheme);
        }
        catch (JSException ex)
        {
            _logger.Error(ex, "JavaScript error toggling theme");
            return Result<Theme, JsInteropError>.Failure(
                JsInteropError.InvocationFailed("toggleTheme", ex.Message),
                ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<ThemeInfo, JsInteropError>> GetThemeInfoAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var ensureResult = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!ensureResult.IsSuccess)
        {
            return Result<ThemeInfo, JsInteropError>.Failure(ensureResult.Error!);
        }

        try
        {
            var themeInfoDict = await _moduleReference!
                .InvokeAsync<Dictionary<string, object>>("getThemeInfo", cancellationToken)
                .ConfigureAwait(false);

            var themeInfo = ThemeInfo.FromDictionary(themeInfoDict);
            return Result<ThemeInfo, JsInteropError>.Success(themeInfo);
        }
        catch (JSException ex)
        {
            _logger.Error(ex, "JavaScript error getting theme info");
            return Result<ThemeInfo, JsInteropError>.Failure(
                JsInteropError.InvocationFailed("getThemeInfo", ex.Message),
                ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<bool, JsInteropError>> ApplyThemeToElementAsync(
        ElementReference elementReference,
        Theme theme,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var ensureResult = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!ensureResult.IsSuccess)
        {
            return Result<bool, JsInteropError>.Failure(ensureResult.Error!);
        }

        try
        {
            var themeString = theme.ToString().ToLowerInvariant();
            var success = await _moduleReference!
                .InvokeAsync<bool>("applyThemeToElement", cancellationToken, elementReference, themeString)
                .ConfigureAwait(false);

            if (success)
            {
                _logger.Debug("Applied theme {Theme} to element", theme);
            }
            else
            {
                _logger.Warning("Failed to apply theme {Theme} to element", theme);
            }

            return Result<bool, JsInteropError>.Success(success);
        }
        catch (JSException ex)
        {
            _logger.Error(ex, "JavaScript error applying theme to element");
            return Result<bool, JsInteropError>.Failure(
                JsInteropError.InvocationFailed("applyThemeToElement", ex.Message),
                ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<bool, JsInteropError>> RemoveThemeFromElementAsync(
        ElementReference elementReference,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var ensureResult = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (!ensureResult.IsSuccess)
        {
            return Result<bool, JsInteropError>.Failure(ensureResult.Error!);
        }

        try
        {
            var success = await _moduleReference!
                .InvokeAsync<bool>("removeThemeFromElement", cancellationToken, elementReference)
                .ConfigureAwait(false);

            if (success)
            {
                _logger.Debug("Removed theme override from element");
            }
            else
            {
                _logger.Warning("Failed to remove theme override from element");
            }

            return Result<bool, JsInteropError>.Success(success);
        }
        catch (JSException ex)
        {
            _logger.Error(ex, "JavaScript error removing theme from element");
            return Result<bool, JsInteropError>.Failure(
                JsInteropError.InvocationFailed("removeThemeFromElement", ex.Message),
                ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (_moduleReference is not null)
            {
                try
                {
                    await _moduleReference.InvokeVoidAsync("dispose").ConfigureAwait(false);
                }
                catch (JSException ex)
                {
                    _logger.Warning(ex, "Error calling dispose on theme module");
                }

                await _moduleReference.DisposeAsync().ConfigureAwait(false);
                _moduleReference = null;
            }

            _initializationLock.Dispose();
            _logger.Information("ThemeService disposed");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during ThemeService disposal");
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Ensures the service is initialized before invoking JavaScript functions.
    /// </summary>
    private async ValueTask<Result<bool, JsInteropError>> EnsureInitializedAsync(
        CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return Result<bool, JsInteropError>.Success(true);
        }

        var initResult = await InitializeAsync(cancellationToken).ConfigureAwait(false);
        return initResult.IsSuccess
            ? Result<bool, JsInteropError>.Success(true)
            : Result<bool, JsInteropError>.Failure(initResult.Error!);
    }

    /// <summary>
    ///     Parses a theme string to a Theme enum value.
    /// </summary>
    private static Theme ParseTheme(string themeString)
    {
        return themeString.ToLowerInvariant() switch
        {
            "light" => Theme.Light,
            "dark" => Theme.Dark,
            "auto" => Theme.Auto,
            _ => Theme.Auto
        };
    }

    /// <summary>
    ///     Throws an ObjectDisposedException if this instance has been disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed([CallerMemberName] string? caller = null)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            throw new ObjectDisposedException(
                GetType().Name,
                $"Cannot {caller} on disposed {GetType().Name}");
        }
    }

    #endregion
}
