#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Menus;

/// <summary>
///     A Blazor component for displaying a dynamic context menu with JavaScript interop.
///     Optimized for Blazor Server with proper thread safety and state management.
/// </summary>
public sealed partial class DropBearContextMenu : DropBearComponentBase
{
    private const string ModuleName = JsModuleNames.ContextMenu;

    private readonly string _contextMenuId = $"context-menu-{Guid.NewGuid()}";
    private readonly SemaphoreSlim _menuStateSemaphore = new(1, 1);
    private DotNetObjectReference<DropBearContextMenu>? _dotNetRef;
    private volatile bool _isInitialized;
    private volatile bool _isVisible;

    private IJSObjectReference? _jsModule;
    private double _left;
    private double _top;
    private ElementReference _triggerElement;

    [Parameter] public IReadOnlyCollection<ContextMenuItem> MenuItems { get; set; } = Array.Empty<ContextMenuItem>();
    [Parameter] public EventCallback<(ContextMenuItem, object?)> OnItemClicked { get; set; }
    [Parameter] public Func<object?>? GetContext { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

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

    [JSInvokable]
    public async Task Show(double left, double top)
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
                _left = left;
                _top = top;
                _isVisible = true;
            });

            LogDebug("Menu shown at [{Left}, {Top}]", left, top);
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
            LogDebug("Item clicked: {Text} with context: {Context}", item.Text, context);

            if (OnItemClicked.HasDelegate)
            {
                await OnItemClicked.InvokeAsync((item, context));
            }
        }
        catch (Exception ex)
        {
            LogError("Error handling menu item click", ex);
        }
        finally
        {
            _menuStateSemaphore.Release();
            await Hide();
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
                    await _jsModule.InvokeVoidAsync(
                        $"{ModuleName}API.dispose",
                        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token,
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
        }
    }
}
