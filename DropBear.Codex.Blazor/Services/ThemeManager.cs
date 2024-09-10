#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Events.EventArgs;
using DropBear.Codex.Core.Logging;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

/// <summary>
///     Manages theme changes within the application and communicates with JavaScript.
/// </summary>
public sealed class ThemeManager : DropBearComponentBase, IAsyncDisposable
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ThemeManager>();
    private readonly IJSRuntime _jsRuntime;
    private Func<Func<Task>, Task> _invokeAsync;
    private DotNetObjectReference<ThemeManager>? _objRef;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ThemeManager" /> class.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to interact with the JavaScript environment.</param>
    public ThemeManager(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _invokeAsync = async action => await action();
        Logger.Information("ThemeManager initialized.");
    }

    /// <summary>
    ///     Disposes of the theme manager and its JavaScript resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("DropBearThemeManager.dispose");
            _objRef?.Dispose();
            Logger.Information("ThemeManager disposed.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing ThemeManager.");
        }
    }

    /// <summary>
    ///     Called after the component is rendered.
    /// </summary>
    /// <param name="firstRender">True if this is the first render.</param>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _objRef = DotNetObjectReference.Create(this);
                await _jsRuntime.InvokeVoidAsync("DropBearThemeManager.initialize", _objRef);
                Logger.Information("ThemeManager initialized in JavaScript.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error initializing ThemeManager JavaScript.");
            }
        }
    }

    /// <summary>
    ///     Invoked from JavaScript when the theme is changed.
    /// </summary>
    /// <param name="effectiveTheme">The effective theme in use.</param>
    /// <param name="userPreference">The user's theme preference.</param>
    [JSInvokable]
    public async Task OnThemeChanged(string effectiveTheme, string userPreference)
    {
        try
        {
            Logger.Information("Theme changed - Effective: {EffectiveTheme}, User Preference: {UserPreference}",
                effectiveTheme, userPreference);

            await _invokeAsync(async () =>
            {
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(effectiveTheme, userPreference));
                Logger.Debug("ThemeChanged event invoked.");
                await Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error processing theme change event.");
        }
    }

    /// <summary>
    ///     Sets the asynchronous invoker for theme change events.
    /// </summary>
    /// <param name="invokeAsync">A function to invoke the event asynchronously.</param>
    public void SetInvokeAsync(Func<Func<Task>, Task> invokeAsync)
    {
        _invokeAsync = invokeAsync ?? throw new ArgumentNullException(nameof(invokeAsync));
        Logger.Information("InvokeAsync method set.");
    }

    /// <summary>
    ///     Event triggered when the theme changes.
    /// </summary>
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    ///     Toggles between the available themes.
    /// </summary>
    public async Task ToggleThemeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("DropBearThemeManager.toggleTheme");
            Logger.Information("Theme toggled.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error toggling theme.");
        }
    }

    /// <summary>
    ///     Sets the theme to the specified value.
    /// </summary>
    /// <param name="theme">The theme to set.</param>
    public async Task SetThemeAsync(string theme)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("DropBearThemeManager.setTheme", theme);
            Logger.Information("Theme set to {Theme}.", theme);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error setting theme to {Theme}.", theme);
        }
    }

    /// <summary>
    ///     Gets the current theme from JavaScript.
    /// </summary>
    /// <returns>The current theme as a string.</returns>
    public async Task<string> GetCurrentThemeAsync()
    {
        try
        {
            var currentTheme = await _jsRuntime.InvokeAsync<string>("DropBearThemeManager.getCurrentTheme");
            Logger.Information("Current theme retrieved: {CurrentTheme}.", currentTheme);
            return currentTheme;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting the current theme.");
            return string.Empty;
        }
    }
}
