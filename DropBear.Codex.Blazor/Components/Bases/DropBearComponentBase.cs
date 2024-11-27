#region

using DropBear.Codex.Blazor.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
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
    private readonly CancellationTokenSource _circuitCts = new();
    private CircuitHandler? _circuitHandler;
    private bool? _isConnected;

    /// <summary>
    ///     Gets or sets the JS Runtime instance.
    /// </summary>
    [Inject]
    protected IJSRuntime JsRuntime { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the logger instance.
    /// </summary>
    [Inject]
    protected ILogger Logger { get; set; } = default!;

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

    /// <summary>
    ///     Gets the cancellation token that is canceled when the circuit is disconnected.
    /// </summary>
    protected CancellationToken CircuitToken => _circuitCts.Token;

    /// <summary>
    ///     Disposes the component and its resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Initializes the component.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Create and initialize circuit handler
            _circuitHandler = new ComponentCircuitHandler(this);

            await base.OnInitializedAsync();
            _isConnected = true;
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
            // Use the circuit token to cancel operations when disconnected
            return await JsRuntime.InvokeAsync<T>(identifier, CircuitToken, args)
                .WaitAsync(TimeSpan.FromSeconds(5), CircuitToken); // Add timeout
        }
        catch (Exception ex) when (ex is JSDisconnectedException
                                       or OperationCanceledException
                                       or TimeoutException)
        {
            _isConnected = false;
            Logger.Warning("JS interop unavailable in {ComponentName}: {Reason}",
                GetType().Name, ex.Message);
            throw new JSDisconnectedException("Circuit is disconnected");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "JS interop call failed in {ComponentName}: {Identifier}",
                GetType().Name, identifier);
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
            await JsRuntime.InvokeVoidAsync(identifier, CircuitToken, args)
                .WaitAsync(TimeSpan.FromSeconds(5), CircuitToken);
        }
        catch (Exception ex) when (ex is JSDisconnectedException
                                       or OperationCanceledException
                                       or TimeoutException)
        {
            _isConnected = false;
            Logger.Warning("JS interop unavailable in {ComponentName}: {Reason}",
                GetType().Name, ex.Message);
            throw new JSDisconnectedException("Circuit is disconnected");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "JS interop call failed in {ComponentName}: {Identifier}",
                GetType().Name, identifier);
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

            // Use a more reliable connection check
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            _isConnected = await JsRuntime.InvokeAsync<bool>(
                "eval",
                cts.Token,
                "typeof window !== 'undefined' && window.document !== null"
            );
            return _isConnected.Value;
        }
        catch (Exception ex) when (ex is JSDisconnectedException
                                       or OperationCanceledException
                                       or TimeoutException)
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
                    // Only attempt JS cleanup if we're still connected
                    if (IsConnected && !_circuitCts.IsCancellationRequested)
                    {
                        await CleanupJavaScriptResourcesAsync()
                            .WaitAsync(TimeSpan.FromSeconds(2)) // Add timeout
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is JSDisconnectedException
                                               or OperationCanceledException
                                               or TimeoutException)
                {
                    Logger.Debug("Skipping JS cleanup for {ComponentName} - circuit disconnected",
                        GetType().Name);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error during {ComponentName} disposal", GetType().Name);
                }
                finally
                {
                    _circuitCts.Dispose();
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

    /// <summary>
    ///     Attempts to reconnect to the circuit with exponential backoff.
    /// </summary>
    protected virtual async Task AttemptReconnectionAsync()
    {
        if (!IsConnected && !CircuitToken.IsCancellationRequested)
        {
            try
            {
                for (var i = 0; i < 3; i++)
                {
                    if (await CheckConnectionStatus())
                    {
                        await OnReconnectedAsync();
                        return;
                    }

                    await Task.Delay(1000 * (i + 1), CircuitToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Error(ex, "Reconnection attempt failed in {ComponentName}", GetType().Name);
            }
        }
    }

    /// <summary>
    ///     Called when the component successfully reconnects to the circuit.
    /// </summary>
    protected virtual Task OnReconnectedAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Inner class that handles circuit events
    /// </summary>
    private class ComponentCircuitHandler : CircuitHandler
    {
        private readonly DropBearComponentBase _component;

        public ComponentCircuitHandler(DropBearComponentBase component)
        {
            _component = component;
        }

        public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            _component._isConnected = true;
            _component.Logger.Debug("Circuit opened in {ComponentName}", _component.GetType().Name);
            return Task.CompletedTask;
        }

        public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            _component._isConnected = false;
            _component._circuitCts.Cancel();
            _component.Logger.Debug("Circuit closed in {ComponentName}", _component.GetType().Name);
            return Task.CompletedTask;
        }

        public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            _component._isConnected = true;
            _component.Logger.Debug("Connection up in {ComponentName}", _component.GetType().Name);
            return Task.CompletedTask;
        }

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            _component._isConnected = false;
            _component.Logger.Debug("Connection down in {ComponentName}", _component.GetType().Name);
            return Task.CompletedTask;
        }
    }
}
