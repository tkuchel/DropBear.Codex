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
    #region Lifecycle Methods

    /// <summary>
    ///     Initializes the component by loading the JavaScript module and creating the context menu.
    /// </summary>
    protected override async Task InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            // Acquire the semaphore to protect state initialization.
            await _menuStateSemaphore.WaitAsync(ComponentToken);

            // Create a .NET object reference for JS interop callbacks.
            _dotNetRef = DotNetObjectReference.Create(this);
            _jsModule = await GetJsModuleAsync(ModuleName);

            // Invoke the JavaScript method to create the context menu.
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

    #endregion

    #region Cleanup

    /// <summary>
    ///     Cleans up JavaScript resources by disposing of the JS module and related references.
    /// </summary>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (_jsModule != null)
            {
                // Wait for the semaphore with a timeout.
                await _menuStateSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                try
                {
                    // Create a temporary cancellation token with a 5-second timeout.
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
        catch (Exception ex) when (ex is JSDisconnectedException || ex is TaskCanceledException)
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
            catch (ObjectDisposedException)
            {
                // Already disposed.
            }

            _dotNetRef = null;
            _jsModule = null;
            _isInitialized = false;
            _isVisible = false;
        }
    }

    #endregion

    #region Fields & Constants

    // Name of the JS module to load.
    private const string ModuleName = JsModuleNames.ContextMenu;

    // Unique identifier for this context menu instance.
    private readonly string _contextMenuId = $"context-menu-{Guid.NewGuid()}";

    // Semaphore to synchronize access to menu state.
    private readonly SemaphoreSlim _menuStateSemaphore = new(1, 1);

    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<DropBearContextMenu>? _dotNetRef;
    private ElementReference _triggerElement; // May be used in markup.
    private volatile bool _isInitialized;
    private volatile bool _isVisible;
    private double _left;
    private double _top;

    #endregion

    #region Parameters

    /// <summary>
    ///     The collection of menu items to display.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<ContextMenuItem> MenuItems { get; set; } = Array.Empty<ContextMenuItem>();

    /// <summary>
    ///     Callback invoked when a menu item is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<(ContextMenuItem, object?)> OnItemClicked { get; set; }

    /// <summary>
    ///     A delegate to provide context data when a menu item is clicked.
    /// </summary>
    [Parameter]
    public Func<object?>? GetContext { get; set; }

    /// <summary>
    ///     Optional child content.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    #endregion

    #region JSInvokable Methods

    /// <summary>
    ///     JS-invokable method to show the context menu at the specified coordinates.
    /// </summary>
    /// <param name="left">Left coordinate in pixels.</param>
    /// <param name="top">Top coordinate in pixels.</param>
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

            // Queue the UI update.
            await QueueStateHasChangedAsync(() =>
            {
                _left = left;
                _top = top;
                _isVisible = true;
                return Task.CompletedTask;
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

    /// <summary>
    ///     JS-invokable method to hide the context menu.
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

    #region Private Helper Methods

    /// <summary>
    ///     Displays the context menu using the mouse event coordinates.
    /// </summary>
    /// <param name="e">The mouse event arguments.</param>
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

            // Ensure the JS module is loaded.
            if (_jsModule == null)
            {
                await InitializeComponentAsync();
            }

            // Invoke the JS function to show the menu at the event coordinates.
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

    /// <summary>
    ///     Handles a click on a context menu item.
    /// </summary>
    /// <param name="item">The clicked menu item.</param>
    private async Task OnItemClickAsync(ContextMenuItem item)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            await _menuStateSemaphore.WaitAsync(ComponentToken);

            // Retrieve contextual data if provided.
            var context = GetContext?.Invoke();
            if (context != null)
            {
                LogDebug("Item clicked: {Text} with context: {Context}", item.Text, context);

                if (OnItemClicked.HasDelegate)
                {
                    await OnItemClicked.InvokeAsync((item, context));
                }
            }
        }
        catch (Exception ex)
        {
            LogError("Error handling menu item click", ex);
        }
        finally
        {
            _menuStateSemaphore.Release();
            // Hide the menu after processing the click.
            await Hide();
        }
    }

    #endregion
}
