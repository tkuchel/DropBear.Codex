#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Interfaces;
using DropBear.Codex.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Menus;

/// <summary>
///     A Blazor component for displaying a dynamic context menu.
/// </summary>
public sealed partial class DropBearContextMenu : DropBearComponentBase, IAsyncDisposable
{
    private readonly string _contextMenuId = $"context-menu-{Guid.NewGuid()}";
    private bool _isDisposed;
    private bool _isVisible;
    private bool _jsInitialized;
    private int _left;
    private DotNetObjectReference<DropBearContextMenu>? _objectReference;
    private int _top;
    // ReSharper disable once NotAccessedField.Local
    private ElementReference _triggerElement;

    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    [Inject] private IDynamicContextMenuService? DynamicContextMenuService { get; set; }
    [Inject] private ILogger<DropBearContextMenu> Logger { get; set; } = null!;

    /// <summary>
    ///     The menu items to display in the context menu.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<ContextMenuItem> MenuItems { get; set; } = Array.Empty<ContextMenuItem>();

    /// <summary>
    ///     Triggered when a menu item is clicked.
    /// </summary>
    [Parameter]
    public EventCallback<(ContextMenuItem, object)> OnItemClicked { get; set; }

    /// <summary>
    ///     A function to get the current context when an item is clicked.
    /// </summary>
    [Parameter]
    public Func<object> GetContext { get; set; } = () => new object();

    /// <summary>
    ///     Whether to use a dynamic context menu service for interactions.
    /// </summary>
    [Parameter]
    public bool UseDynamicService { get; set; }

    /// <summary>
    ///     Custom content to be rendered inside the context menu or the trigger element for the menu.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed && _jsInitialized)
        {
            try
            {
                await JsRuntime.InvokeVoidAsync("DropBearContextMenu.dispose", _contextMenuId);
                Logger.LogInformation("Context menu JavaScript resources disposed for {ContextMenuId}", _contextMenuId);
            }
            catch (JSException ex)
            {
                Logger.LogError(ex, "Failed to dispose context menu JavaScript resources");
            }
        }

        _objectReference?.Dispose();
        _isDisposed = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _objectReference = DotNetObjectReference.Create(this);
            await InitializeJavaScriptAsync();
        }
    }

    private async Task InitializeJavaScriptAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("DropBearContextMenu.initialize", _contextMenuId, _objectReference);
            _jsInitialized = true;
            Logger.LogInformation("ContextMenu initialized with ID: {ContextMenuId}", _contextMenuId);
        }
        catch (JSException ex)
        {
            Logger.LogError(ex, "Failed to initialize JavaScript for ContextMenu");
            _jsInitialized = false;
        }
    }

    [JSInvokable]
    public void Show(double left, double top)
    {
        if (_isDisposed)
        {
            Logger.LogWarning("Attempted to show disposed context menu.");
            return;
        }

        _left = (int)Math.Round(left);
        _top = (int)Math.Round(top);
        _isVisible = true;
        Logger.LogDebug("ContextMenu shown at coordinates: Left={Left}, Top={Top}", _left, _top);
        StateHasChanged();
    }

    [JSInvokable]
    public void Hide()
    {
        _isVisible = false;
        Logger.LogDebug("ContextMenu hidden");
        StateHasChanged();
    }

    private async Task OnContextMenu(MouseEventArgs e)
    {
        if (_isDisposed)
        {
            Logger.LogWarning("Attempted to show context menu, but component is disposed.");
            return;
        }

        if (!_jsInitialized)
        {
            Logger.LogWarning("Attempted to show context menu, but JavaScript is not initialized");
            return;
        }

        try
        {
            await JsRuntime.InvokeVoidAsync("DropBearContextMenu.show", _contextMenuId, e.ClientX, e.ClientY);
        }
        catch (JSException ex)
        {
            Logger.LogError(ex, "Failed to show context menu");
        }
    }

    private async Task OnItemClick(ContextMenuItem item)
    {
        if (_isDisposed)
        {
            Logger.LogWarning("Attempted to handle click on a disposed context menu.");
            return;
        }

        var context = GetContext();
        Logger.LogInformation("Menu item clicked: {ItemText} with context: {Context}", item.Text, context);

        await OnItemClicked.InvokeAsync((item, context));
        Hide();
    }
}
