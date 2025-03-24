#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Menus;

/// <summary>
///     A Blazor component that provides a context menu triggered by right-clicking on content.
///     Supports nested submenus, item icons, and custom positioning.
/// </summary>
public partial class DropBearContextMenu : DropBearComponentBase
{
    #region Fields & Constants

    private const string ModuleName = JsModuleNames.ContextMenu;
    private readonly string _contextMenuId = $"context-menu-{Guid.NewGuid()}";
    private readonly SemaphoreSlim _menuStateSemaphore = new(1, 1);
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<DropBearContextMenu>? _dotNetRef;
    private ElementReference _triggerElement;
    private volatile bool _isInitialized;
    private bool _isVisible;
    private double _left;
    private double _top;

    // Backing fields for parameters
    private IReadOnlyCollection<ContextMenuItem> _menuItems = Array.Empty<ContextMenuItem>();
    private EventCallback<(ContextMenuItem, object?)> _onItemClicked;
    private Func<object?>? _getContext;
    private RenderFragment? _childContent;

    // Flag to track if component should render
    private bool _shouldRender = true;

    // Window dimensions cache
    private WindowDimensions? _cachedDimensions;
    private DateTime _lastDimensionsCheck = DateTime.MinValue;
    private static readonly TimeSpan DimensionsCacheDuration = TimeSpan.FromSeconds(1);

    #endregion

    #region Parameters

    /// <summary>
    ///     Gets or sets the collection of menu items to display in the context menu.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<ContextMenuItem> MenuItems
    {
        get => _menuItems;
        set
        {
            if (!ReferenceEquals(_menuItems, value))
            {
                _menuItems = value;
                _shouldRender = true;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the callback that is invoked when a menu item is clicked.
    ///     Provides both the clicked item and the optional context object.
    /// </summary>
    [Parameter]
    public EventCallback<(ContextMenuItem, object?)> OnItemClicked
    {
        get => _onItemClicked;
        set
        {
            if (_onItemClicked.Equals(value))
            {
                return;
            }

            _onItemClicked = value;
        }
    }

    /// <summary>
    ///     Gets or sets a function that returns a context object to be passed to the OnItemClicked callback.
    ///     This allows for dynamic context to be provided at the time of interaction.
    /// </summary>
    [Parameter]
    public Func<object?>? GetContext
    {
        get => _getContext;
        set
        {
            if (_getContext == value)
            {
                return;
            }

            _getContext = value;
        }
    }

    /// <summary>
    ///     Gets or sets the content that will trigger the context menu when right-clicked.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent
    {
        get => _childContent;
        set
        {
            if (_childContent == value)
            {
                return;
            }

            _childContent = value;
            _shouldRender = true;
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    ///     Controls whether the component should render, optimizing for performance.
    /// </summary>
    /// <returns>True if the component should render, false otherwise.</returns>
    protected override bool ShouldRender()
    {
        if (_shouldRender)
        {
            _shouldRender = false;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Initializes the component by creating and configuring the JavaScript context menu.
    /// </summary>
    protected override async ValueTask InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _menuStateSemaphore.WaitAsync(ComponentToken);

            _dotNetRef = DotNetObjectReference.Create(this);
            var jsModuleResult = await GetJsModuleAsync(ModuleName);

            if (jsModuleResult.IsFailure)
            {
                LogError("Failed to get JS module: {Error}", jsModuleResult.Exception);
                return;
            }

            _jsModule = jsModuleResult.Value;

            await _jsModule.InvokeVoidAsync(
                $"{ModuleName}API.createContextMenu",
                ComponentToken,
                _contextMenuId,
                _dotNetRef
            );

            _isInitialized = true;
            LogDebug("Context menu initialized: {MenuId}", _contextMenuId);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize context menu", ex);
            throw;
        }
        finally
        {
            _menuStateSemaphore.Release();
        }
    }

    /// <summary>
    ///     Cleans up JavaScript resources when the component is disposed.
    /// </summary>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (_jsModule != null)
            {
                await _menuStateSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _jsModule.InvokeVoidAsync(
                        $"{ModuleName}API.dispose",
                        cts.Token,
                        _contextMenuId
                    );
                    LogDebug("Context menu resources cleaned up");
                }
                finally
                {
                    _menuStateSemaphore.Release();
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to cleanup context menu", ex);
        }
        finally
        {
            try
            {
                _dotNetRef?.Dispose();
                _menuStateSemaphore.Dispose();
            }
            catch (ObjectDisposedException) { }

            _dotNetRef = null;
            _jsModule = null;
            _isInitialized = false;
            _isVisible = false;
            _cachedDimensions = null;
        }
    }

    #endregion

    #region Menu Position Handling

    /// <summary>
    ///     Gets the current window dimensions, using a cached value if available and not expired.
    /// </summary>
    /// <returns>A WindowDimensions object containing the width and height of the window.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<WindowDimensions> GetWindowDimensionsAsync()
    {
        if (_cachedDimensions != null && DateTime.UtcNow - _lastDimensionsCheck < DimensionsCacheDuration)
        {
            return _cachedDimensions;
        }

        try
        {
            // Get the Utils module instead of trying to use the context menu module
            var utilsModuleResult = await GetJsModuleAsync(JsModuleNames.Utils);

            if (utilsModuleResult.IsFailure)
            {
                LogError("Failed to get Utils module: {Error}", utilsModuleResult.Exception);
                throw new InvalidOperationException("Failed to get Utils module");
            }

            var utilsModule = utilsModuleResult.Value;

            _cachedDimensions = await utilsModule.InvokeAsync<WindowDimensions>(
                $"{JsModuleNames.Utils}API.getWindowDimensions",
                ComponentToken
            );
            _lastDimensionsCheck = DateTime.UtcNow;

            return _cachedDimensions;
        }
        catch (Exception ex)
        {
            LogError("Failed to get window dimensions", ex);
            throw;
        }
    }

    /// <summary>
    ///     Updates the menu position to ensure it stays within the viewport boundaries.
    /// </summary>
    /// <param name="x">The initial X coordinate.</param>
    /// <param name="y">The initial Y coordinate.</param>
    private async Task UpdateMenuPositionAsync(double x, double y)
    {
        var dimensions = await GetWindowDimensionsAsync();

        // Calculate optimal position
        var left = x;
        var top = y;

        // Menu dimensions (can be made configurable)
        const int menuWidth = 200;
        const int menuHeight = 300;
        const int margin = 10;

        // Adjust for right edge
        if (left + menuWidth > dimensions.Width)
        {
            left = dimensions.Width - menuWidth - margin;
        }

        // Adjust for bottom edge
        if (top + menuHeight > dimensions.Height)
        {
            top = dimensions.Height - menuHeight - margin;
        }

        // Ensure minimum margins
        left = Math.Max(margin, left);
        top = Math.Max(margin, top);

        _left = left;
        _top = top;
    }

    #endregion

    #region JSInvokable Methods

    /// <summary>
    ///     Shows the context menu at the specified position. Called from JavaScript.
    /// </summary>
    /// <param name="x">The X coordinate where the menu should appear.</param>
    /// <param name="y">The Y coordinate where the menu should appear.</param>
    [JSInvokable]
    public async Task Show(double x, double y)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            await _menuStateSemaphore.WaitAsync(ComponentToken);

            await UpdateMenuPositionAsync(x, y);

            await QueueStateHasChangedAsync(() =>
            {
                _isVisible = true;
                _shouldRender = true;
                return Task.CompletedTask;
            });

            LogDebug("Menu shown at [{Left}, {Top}]", _left, _top);
        }
        catch (Exception ex)
        {
            LogError("Failed to show menu", ex);
        }
        finally
        {
            _menuStateSemaphore.Release();
        }
    }

    /// <summary>
    ///     Hides the context menu. Called from JavaScript when clicking outside or pressing Escape.
    /// </summary>
    [JSInvokable]
    public async Task Hide()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            await _menuStateSemaphore.WaitAsync(ComponentToken);

            await QueueStateHasChangedAsync(() =>
            {
                _isVisible = false;
                _shouldRender = true;
                return Task.CompletedTask;
            });

            LogDebug("Menu hidden: {MenuId}", _contextMenuId);
        }
        catch (Exception ex)
        {
            LogError("Failed to hide menu", ex);
        }
        finally
        {
            _menuStateSemaphore.Release();
        }
    }

    #endregion

    #region Event Handling

    /// <summary>
    ///     Shows the context menu in response to a right-click event.
    /// </summary>
    /// <param name="e">The mouse event arguments containing cursor position.</param>
    private async Task ShowContextMenuAsync(MouseEventArgs e)
    {
        if (!_isInitialized || IsDisposed)
        {
            LogWarning("Cannot show menu - not initialized or disposed");
            return;
        }

        try
        {
            await _menuStateSemaphore.WaitAsync(ComponentToken);

            if (_jsModule == null)
            {
                await InitializeComponentAsync();
            }

            await _jsModule!.InvokeVoidAsync(
                $"{ModuleName}API.show",
                ComponentToken,
                _contextMenuId,
                e.ClientX,
                e.ClientY
            );
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Failed to show context menu: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to show context menu", ex);
            await Hide();
        }
        finally
        {
            _menuStateSemaphore.Release();
        }
    }

    /// <summary>
    ///     Handles clicking on a menu item by invoking the callback and hiding the menu.
    /// </summary>
    /// <param name="item">The menu item that was clicked.</param>
    private async Task OnItemClickAsync(ContextMenuItem item)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            await _menuStateSemaphore.WaitAsync(ComponentToken);

            var context = _getContext?.Invoke();

            if (_onItemClicked.HasDelegate)
            {
                await _onItemClicked.InvokeAsync((item, context));
            }

            // Don't auto-hide if item has submenu
            if (!item.HasSubmenu)
            {
                await Hide();
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            LogWarning("Error handling menu item click: {Reason}", ex.GetType().Name);
            await Hide();
        }
        catch (Exception ex)
        {
            LogError("Error handling menu item click", ex);
            await Hide();
        }
        finally
        {
            _menuStateSemaphore.Release();
        }
    }

    #endregion
}
