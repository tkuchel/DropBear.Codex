#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Menus;

/// <summary>
///     A Blazor component for displaying a dynamic context menu.
/// </summary>
public sealed partial class DropBearContextMenu : DropBearComponentBase
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearContextMenu>();
    private readonly string _contextMenuId = $"context-menu-{Guid.NewGuid()}";
    private bool _isVisible;
    private bool _jsInitialized;
    private double _left;
    private DotNetObjectReference<DropBearContextMenu>? _objectReference;
    private double _top;

    private ElementReference _triggerElement;


    /// <summary>
    ///     The menu items to display in the context menu.
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<ContextMenuItem> MenuItems { get; set; } = Array.Empty<ContextMenuItem>();

    /// <summary>
    ///     Triggered when a menu item is clicked.
    ///     The parameter is a tuple: (ContextMenuItem, contextObject).
    /// </summary>
    [Parameter]
    public EventCallback<(ContextMenuItem, object?)> OnItemClicked { get; set; }

    /// <summary>
    ///     A function to obtain the current context when an item is clicked.
    /// </summary>
    [Parameter]
    public Func<object?>? GetContext { get; set; }

    /// <summary>
    ///     Custom content to be rendered inside the trigger element (and optionally within the context menu).
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Disposes JavaScript resources and DotNetObjectReference.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (_jsInitialized)
        {
            try
            {
                await JsRuntime.InvokeVoidAsync("DropBearContextMenu.dispose", _contextMenuId);
                Logger.Debug("Context menu JS resources disposed for {Id}", _contextMenuId);
            }
            catch (JSDisconnectedException ex)
            {
                Logger.Warning(ex, "Failed to dispose context menu JS resources: disconnected circuit.");
            }
            catch (JSException ex)
            {
                Logger.Error(ex, "Failed to dispose context menu JS resources.");
            }
        }

        _objectReference?.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            _objectReference = DotNetObjectReference.Create(this);
            await InitializeJavaScriptAsync();
        }
    }

    /// <summary>
    ///     Initializes the JavaScript interop for this context menu.
    /// </summary>
    private async Task InitializeJavaScriptAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("DropBearContextMenu.initialize", _contextMenuId, _objectReference);
            _jsInitialized = true;
            Logger.Debug("ContextMenu initialized with ID: {Id}", _contextMenuId);
        }
        catch (JSException ex)
        {
            Logger.Error(ex, "Failed to initialize JavaScript for ContextMenu.");
            _jsInitialized = false;
        }
    }

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
        Logger.Debug("ContextMenu shown at [Left={Left}, Top={Top}]", left, top);
        StateHasChanged();
    }

    /// <summary>
    ///     Hides the context menu.
    ///     Invoked from JavaScript via DotNet.
    /// </summary>
    [JSInvokable]
    public void Hide()
    {
        _isVisible = false;
        Logger.Debug("ContextMenu hidden.");
        StateHasChanged();
    }

    /// <summary>
    ///     Called when user right-clicks on the trigger element to show the context menu.
    /// </summary>
    private async Task ShowContextMenuAsync(MouseEventArgs e)
    {
        if (!_jsInitialized)
        {
            Logger.Warning("Attempted to show context menu, but JS is not initialized.");
            return;
        }

        try
        {
            await JsRuntime.InvokeVoidAsync("DropBearContextMenu.show", _contextMenuId, e.ClientX, e.ClientY);
        }
        catch (JSException ex)
        {
            Logger.Error(ex, "Failed to show context menu via JS.");
        }
    }

    /// <summary>
    ///     Called when a menu item (or sub-item) is clicked.
    ///     Hides the menu and invokes the OnItemClicked callback with the current context.
    /// </summary>
    private async Task OnItemClickAsync(ContextMenuItem item)
    {
        var context = GetContext?.Invoke();
        Logger.Information("Menu item clicked: {Text}, with context: {Context}", item.Text, context);

        if (OnItemClicked.HasDelegate)
        {
            await OnItemClicked.InvokeAsync((item, context));
        }

        Hide();
    }
}
