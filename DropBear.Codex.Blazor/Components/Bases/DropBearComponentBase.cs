#region

using DropBear.Codex.Blazor.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Bases;

/// <summary>
///     An abstract base class for all DropBear components that provides common functionality
///     for handling component lifecycle, JS interop, and disposal patterns.
/// </summary>
public abstract class DropBearComponentBase : ComponentBase, IAsyncDisposable
{
    internal readonly CancellationTokenSource CircuitCts = new();
    internal bool IsConnected { get; set; } = true;

    #region Helpers

    private void LogCircuitDisconnection(string message)
    {
        Logger.Debug("{Message} in {ComponentName}", message, GetType().Name);
    }

    #endregion

    #region Injected Services

    [Inject] protected IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] protected internal ILogger Logger { get; set; } = default!;

    #endregion

    #region Properties

    protected string ComponentId { get; } = $"dropbear-{Guid.NewGuid():N}";
    private int _isDisposed; // Backing field for disposal state

    /// <summary>
    ///     Gets a value indicating whether the component has been disposed.
    /// </summary>
    protected bool IsDisposed => _isDisposed == 1;

    private CancellationToken CircuitToken => CircuitCts.Token;

    #endregion

    #region Lifecycle Management

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await base.OnInitializedAsync();
            IsConnected = true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during {ComponentName} initialization", GetType().Name);
            throw;
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1) // Ensure single disposal
        {
            return;
        }

        if (disposing)
        {
            try
            {
                if (IsConnected && !CircuitCts.IsCancellationRequested)
                {
                    await CleanupJavaScriptResourcesAsync()
                        .WaitAsync(TimeSpan.FromSeconds(2), CircuitToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                LogCircuitDisconnection("Circuit disconnected; skipping JS cleanup");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during {ComponentName} disposal", GetType().Name);
            }
            finally
            {
                CircuitCts.Dispose();
            }
        }
    }

    #endregion

    #region JavaScript Interop

    protected async Task<T> SafeJsInteropAsync<T>(string identifier, params object[] args)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        try
        {
            return await JsRuntime.InvokeAsync<T>(identifier, CircuitToken, args)
                .WaitAsync(TimeSpan.FromSeconds(5), CircuitToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException or TimeoutException)
        {
            IsConnected = false;
            Logger.Warning("JS interop unavailable in {ComponentName}: {Reason}", GetType().Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "JS interop call failed in {ComponentName}: {Identifier}", GetType().Name, identifier);
            throw;
        }
    }

    protected async Task SafeJsVoidInteropAsync(string identifier, params object[] args)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        try
        {
            await JsRuntime.InvokeVoidAsync(identifier, CircuitToken, args)
                .WaitAsync(TimeSpan.FromSeconds(5), CircuitToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException or TimeoutException)
        {
            IsConnected = false;
            Logger.Warning("JS interop unavailable in {ComponentName}: {Reason}", GetType().Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "JS interop call failed in {ComponentName}: {Identifier}", GetType().Name, identifier);
            throw;
        }
    }

    protected virtual Task CleanupJavaScriptResourcesAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region State Management

    protected async Task InvokeStateHasChangedAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error executing state change in {ComponentName}", GetType().Name);
            throw;
        }
    }

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

    #endregion
}
