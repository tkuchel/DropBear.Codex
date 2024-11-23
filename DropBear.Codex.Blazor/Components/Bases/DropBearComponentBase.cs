#region

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Components.Bases;

/// <summary>
///     An abstract base class for all DropBear components that provides common functionality
///     for handling component lifecycle, JS interop, and disposal patterns.
/// </summary>
public abstract class DropBearComponentBase : ComponentBase, IAsyncDisposable
{
    private bool? _isConnected;

    /// <summary>
    ///     Gets or sets the JS Runtime instance.
    /// </summary>
    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the logger instance.
    /// </summary>
    [Inject]
    protected Serilog.ILogger Logger { get; set; } = default!;

    /// <summary>
    ///     Gets the unique identifier for the component instance.
    /// </summary>
    protected string ComponentId { get; } = $"dropbear-{Guid.NewGuid():N}";

    /// <summary>
    ///     Gets a value indicating whether the component has been disposed.
    /// </summary>
    protected bool IsDisposed { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the Blazor circuit is currently connected.
    /// </summary>
    protected bool IsConnected => _isConnected ?? true;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            await CheckConnectionStatus();
            await base.OnInitializedAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during {ComponentName} initialization", GetType().Name);
            throw;
        }
    }

    /// <summary>
    ///     Safely invokes JavaScript interop methods with connection and disposal checks.
    /// </summary>
    /// <typeparam name="T">The return type of the JavaScript function.</typeparam>
    /// <param name="identifier">The identifier of the JavaScript function to invoke.</param>
    /// <param name="args">Arguments to pass to the JavaScript function.</param>
    /// <returns>A task representing the result of the JavaScript invocation.</returns>
    protected async Task<T> SafeJsInteropAsync<T>(string identifier, params object[] args)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        try
        {
            if (!await CheckConnectionStatus())
            {
                throw new JSDisconnectedException("Circuit is disconnected");
            }

            return await JSRuntime.InvokeAsync<T>(identifier, args);
        }
        catch (JSDisconnectedException ex)
        {
            _isConnected = false;
            Logger.Warning(ex, "JS interop failed due to disconnection in {ComponentName}", GetType().Name);
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "JS interop call failed in {ComponentName}: {Identifier}", GetType().Name, identifier);
            throw;
        }
    }

    /// <summary>
    ///     Safely invokes void JavaScript interop methods with connection and disposal checks.
    /// </summary>
    /// <param name="identifier">The identifier of the JavaScript function to invoke.</param>
    /// <param name="args">Arguments to pass to the JavaScript function.</param>
    protected async Task SafeJsVoidInteropAsync(string identifier, params object[] args)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        try
        {
            if (!await CheckConnectionStatus())
            {
                throw new JSDisconnectedException("Circuit is disconnected");
            }

            await JSRuntime.InvokeVoidAsync(identifier, args);
        }
        catch (JSDisconnectedException ex)
        {
            _isConnected = false;
            Logger.Warning(ex, "JS interop failed due to disconnection in {ComponentName}", GetType().Name);
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "JS interop call failed in {ComponentName}: {Identifier}", GetType().Name, identifier);
            throw;
        }
    }

    /// <summary>
    ///     Checks if the Blazor circuit is still connected.
    /// </summary>
    private async Task<bool> CheckConnectionStatus()
    {
        try
        {
            if (_isConnected.HasValue)
            {
                return _isConnected.Value;
            }

            // Try a simple JS interop call to check connection
            _isConnected = await JSRuntime.InvokeAsync<bool>("document.hasFocus");
            return _isConnected.Value;
        }
        catch (JSDisconnectedException)
        {
            _isConnected = false;
            return false;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to check connection status in {ComponentName}", GetType().Name);
            _isConnected = false;
            return false;
        }
    }

    /// <summary>
    ///     Performs component cleanup operations.
    /// </summary>
    /// <param name="disposing">True if being called from Dispose.</param>
    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                try
                {
                    if (await CheckConnectionStatus())
                    {
                        await CleanupJavaScriptResourcesAsync();
                    }
                }
                catch (JSDisconnectedException)
                {
                    // Safely ignore JS disconnection exceptions during disposal
                    Logger.Debug("JS interop unavailable during {ComponentName} disposal - circuit disconnected",
                        GetType().Name);
                }
                catch (Exception ex)
                {
                    // Log other exceptions but don't throw during disposal
                    Logger.Error(ex, "Error during {ComponentName} disposal", GetType().Name);
                }
            }

            IsDisposed = true;
        }
    }

    /// <summary>
    ///     Override this method to clean up component-specific JavaScript resources.
    /// </summary>
    protected virtual Task CleanupJavaScriptResourcesAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Ensures component state is updated after executing an action.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    protected async Task InvokeStateHasChangedAsync(Func<Task> action)
    {
        try
        {
            await action();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error executing state change in {ComponentName}", GetType().Name);
            throw;
        }
    }

    /// <summary>
    ///     Ensures component state is updated after executing an action.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    protected void InvokeStateHasChanged(Action action)
    {
        try
        {
            action();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error executing state change in {ComponentName}", GetType().Name);
            throw;
        }
    }
}
