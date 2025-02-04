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
///     Refactored to use DropBearComponentBase for JS lifecycle and disposal.
/// </summary>
public sealed partial class DropBearContextMenu : DropBearComponentBase
{
    private const string MODULE_NAME = JsModuleNames.ContextMenu; // The registered name in your JS "import"

    // Unique ID for the menu element in the DOM
    private readonly string _contextMenuId = $"context-menu-{Guid.NewGuid()}";

    private bool _isVisible;

    private IJSObjectReference? _jsModule;
    private double _left;
    private DotNetObjectReference<DropBearContextMenu>? _objectReference;
    private double _top;

    // If you want a reference to the trigger element
    private ElementReference _triggerElement;

    #region Parameters

    /// <summary>
    ///     The menu items to display in the context menu.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<ContextMenuItem> MenuItems { get; set; } = Array.Empty<ContextMenuItem>();

    /// <summary>
    ///     Triggered when a menu item is clicked. Passes (ContextMenuItem, contextObject).
    /// </summary>
    [Parameter]
    public EventCallback<(ContextMenuItem, object?)> OnItemClicked { get; set; }

    /// <summary>
    ///     A function to obtain the current context when an item is clicked.
    /// </summary>
    [Parameter]
    public Func<object?>? GetContext { get; set; }

    /// <summary>
    ///     Custom content to be rendered inside the trigger element (and optionally in the menu).
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    /// <remarks>
    ///     Loads and initializes the JS module on first render.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || IsDisposed)
        {
            return;
        }

        try
        {
            _objectReference = DotNetObjectReference.Create(this);

            // Ensure the JS module is registered/loaded
            // await EnsureJsModuleInitializedAsync(MODULE_NAME).ConfigureAwait(false);

            // Retrieve the module reference from the base class's cache
            _jsModule = await GetJsModuleAsync(MODULE_NAME).ConfigureAwait(false);

            // Now call the "createContextMenu" method within that module
            await _jsModule.InvokeVoidAsync($"{MODULE_NAME}API.createContextMenu", _contextMenuId, _objectReference)
                .ConfigureAwait(false);

            LogDebug("Context menu JS initialized with ID: {MenuId}", _contextMenuId);
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize ContextMenu JS", ex);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Called during disposal to handle final JS cleanups. The base class calls this from DisposeCoreAsync().
    /// </remarks>
    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            // If the JS was successfully initialized, call the module's dispose function
            if (_jsModule is not null)
            {
                await _jsModule.InvokeVoidAsync($"{MODULE_NAME}API.dispose", _contextMenuId).ConfigureAwait(false);
                LogDebug("Context menu JS resources disposed for {MenuId}", _contextMenuId);
            }
        }
        catch (Exception ex)
        {
            LogWarning("Failed to dispose context menu JS resources for {MenuId}, reason: {Message}", ex,
                _contextMenuId, ex.Message);
        }
        finally
        {
            _objectReference?.Dispose();
            _objectReference = null;
            _jsModule = null;
        }
    }

    #endregion

    #region JSInvokable Methods

    /// <summary>
    ///     Shows the context menu at the specified (left, top) coordinates.
    ///     Invoked from JavaScript via DotNet.
    /// </summary>
    [JSInvokable]
    public void Show(double left, double top)
    {
        _left = left;
        _top = top;
        _isVisible = true;

        LogDebug("Context menu shown at [Left={Left}, Top={Top}]", left, top);
        StateHasChanged();
    }

    /// <summary>
    ///     Hides the context menu. Invoked from JS via DotNet.
    /// </summary>
    [JSInvokable]
    public void Hide()
    {
        _isVisible = false;
        LogDebug("Context menu hidden: {MenuId}", _contextMenuId);
        StateHasChanged();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Called when user right-clicks on the trigger element to show the context menu.
    /// </summary>
    private async Task ShowContextMenuAsync(MouseEventArgs e)
    {
        // If the JS module hasn't been initialized or the component is disposed, skip
        if (IsDisposed || _jsModule is null)
        {
            LogWarning("Attempted to show context menu, but JS is not ready or component is disposed.");
            return;
        }

        // Show the context menu via the JS module
        try
        {
            await _jsModule.InvokeVoidAsync($"{MODULE_NAME}API.show", _contextMenuId, e.ClientX, e.ClientY)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError("Error showing context menu via JS", ex);
        }
    }

    /// <summary>
    ///     Called when a menu item is clicked.
    ///     Hides the menu and invokes OnItemClicked with the current context.
    /// </summary>
    private async Task OnItemClickAsync(ContextMenuItem item)
    {
        try
        {
            var context = GetContext?.Invoke();
            LogDebug("Menu item clicked: {Text}, with context: {Context}", item.Text, context);

            if (OnItemClicked.HasDelegate)
            {
                await OnItemClicked.InvokeAsync((item, context)).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogError("Error in OnItemClickAsync", ex);
        }
        finally
        {
            Hide();
        }
    }

    #endregion
}
