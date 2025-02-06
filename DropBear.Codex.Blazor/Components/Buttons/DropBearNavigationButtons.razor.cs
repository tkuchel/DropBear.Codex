#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Buttons;

/// <summary>
///     A component that renders navigational buttons for going back, going home, and scrolling to top.
///     Optimized for Blazor Server with proper thread synchronization and memory management.
/// </summary>
public sealed partial class DropBearNavigationButtons : DropBearComponentBase
{
    private const string JsModuleName = JsModuleNames.NavigationButtons;
    private readonly SemaphoreSlim _navigationSemaphore = new(1, 1);
    private DotNetObjectReference<DropBearNavigationButtons>? _dotNetRef;
    private bool _isInitialized;
    private volatile bool _isVisible;
    private IJSObjectReference? _module;

    private bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            _ = QueueStateHasChangedAsync(() => { });
        }
    }

    [Parameter] public string BackButtonTop { get; set; } = "20px";
    [Parameter] public string BackButtonLeft { get; set; } = "80px";
    [Parameter] public string HomeButtonTop { get; set; } = "20px";
    [Parameter] public string HomeButtonLeft { get; set; } = "140px";
    [Parameter] public string ScrollTopButtonBottom { get; set; } = "20px";
    [Parameter] public string ScrollTopButtonRight { get; set; } = "20px";

    protected override async Task InitializeComponentAsync()
    {
        if (_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _navigationSemaphore.WaitAsync(ComponentToken);

            _module = await GetJsModuleAsync(JsModuleName);
            await _module.InvokeVoidAsync($"{JsModuleName}API.initialize", ComponentToken);

            _dotNetRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync($"{JsModuleName}API.createNavigationManager",
                ComponentToken, _dotNetRef);

            _isInitialized = true;
            LogDebug("Navigation buttons initialized");
        }
        catch (Exception ex)
        {
            LogError("Failed to initialize navigation buttons", ex);
            throw;
        }
        finally
        {
            _navigationSemaphore.Release();
        }
    }

    private async Task GoBack()
    {
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _navigationSemaphore.WaitAsync(ComponentToken);

            if (_module == null)
            {
                _module = await GetJsModuleAsync(JsModuleName);
            }

            await _module.InvokeVoidAsync($"{JsModuleName}API.goBack", ComponentToken);
            LogDebug("Navigated back");
        }
        catch (Exception ex)
        {
            LogError("Failed to navigate back", ex);
        }
        finally
        {
            _navigationSemaphore.Release();
        }
    }

    private async Task GoHome()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            await InvokeAsync(() => NavigationManager.NavigateTo("/"));
            LogDebug("Navigated home");
        }
        catch (Exception ex)
        {
            LogError("Failed to navigate home", ex);
        }
    }

    private async Task ScrollToTop()
    {
        if (!_isInitialized || IsDisposed)
        {
            return;
        }

        try
        {
            await _navigationSemaphore.WaitAsync(ComponentToken);

            if (_module == null)
            {
                _module = await GetJsModuleAsync(JsModuleName);
            }

            await _module.InvokeVoidAsync($"{JsModuleName}API.scrollToTop", ComponentToken);
            LogDebug("Scrolled to top");
        }
        catch (Exception ex)
        {
            LogError("Failed to scroll to top", ex);
        }
        finally
        {
            _navigationSemaphore.Release();
        }
    }

    [JSInvokable]
    public async Task UpdateVisibility(bool isVisible)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            await InvokeAsync(() => IsVisible = isVisible);
            LogDebug("Visibility updated: {IsVisible}", isVisible);
        }
        catch (Exception ex)
        {
            LogError("Failed to update visibility", ex);
        }
    }

    protected override async Task CleanupJavaScriptResourcesAsync()
    {
        try
        {
            if (_module != null)
            {
                await _navigationSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
                try
                {
                    await _module.InvokeVoidAsync($"{JsModuleName}API.disposeAPI",
                        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
                    LogDebug("Navigation resources cleaned up");
                }
                finally
                {
                    _navigationSemaphore.Release();
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException)
        {
            LogWarning("Cleanup interrupted: {Reason}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            LogError("Failed to cleanup navigation resources", ex);
        }
        finally
        {
            try
            {
                _dotNetRef?.Dispose();
                _navigationSemaphore.Dispose();
            }
            catch (ObjectDisposedException) { }

            _dotNetRef = null;
            _module = null;
            _isInitialized = false;
        }
    }
}
