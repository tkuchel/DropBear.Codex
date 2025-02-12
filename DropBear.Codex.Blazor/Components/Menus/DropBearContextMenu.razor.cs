#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Menus;

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
    private volatile bool _isVisible;
    private double _left;
    private double _top;

    // Window dimensions cache
    private WindowDimensions? _cachedDimensions;
    private DateTime _lastDimensionsCheck = DateTime.MinValue;
    private static readonly TimeSpan DimensionsCacheDuration = TimeSpan.FromSeconds(1);

    #endregion

    #region Parameters

    [Parameter] public IReadOnlyCollection<ContextMenuItem> MenuItems { get; set; } = Array.Empty<ContextMenuItem>();

    [Parameter] public EventCallback<(ContextMenuItem, object?)> OnItemClicked { get; set; }

    [Parameter] public Func<object?>? GetContext { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    #endregion

    #region Lifecycle Methods

    protected override async Task InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _menuStateSemaphore.WaitAsync(ComponentToken);

            _dotNetRef = DotNetObjectReference.Create(this);
            _jsModule = await GetJsModuleAsync(ModuleName);

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

    private async Task<WindowDimensions> GetWindowDimensionsAsync()
    {
        if (_cachedDimensions != null && DateTime.UtcNow - _lastDimensionsCheck < DimensionsCacheDuration)
        {
            return _cachedDimensions;
        }

        try
        {
            if (_jsModule == null)
            {
                throw new InvalidOperationException("JS module not initialized");
            }

            _cachedDimensions = await _jsModule.InvokeAsync<WindowDimensions>(
                $"{ModuleName}API.getWindowDimensions",
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

    private async Task OnItemClickAsync(ContextMenuItem item)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            await _menuStateSemaphore.WaitAsync(ComponentToken);

            var context = GetContext?.Invoke();

            if (OnItemClicked.HasDelegate)
            {
                await OnItemClicked.InvokeAsync((item, context));
            }

            // Don't auto-hide if item has submenu
            if (!item.HasSubmenu)
            {
                await Hide();
            }
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
