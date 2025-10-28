#region

using System.Diagnostics;
using System.Reflection;
using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Extensions;

/// <summary>
///     Provides extension methods for Blazor components to enhance circuit connection resilience.
/// </summary>
public static class CircuitResilienceExtensions
{
    /// <summary>
    ///     Waits for the circuit connection to be established or restored.
    /// </summary>
    /// <param name="component">The component base to extend.</param>
    /// <param name="timeout">Maximum time to wait for the connection.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if the connection was established within the timeout; otherwise, false.</returns>
    public static async Task<bool> WaitForCircuitConnectionAsync(
        this DropBearComponentBase component,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // Access the IsConnected property using reflection since it's protected
        var isConnectedProperty = component.GetType().GetProperty("IsConnected",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (isConnectedProperty == null)
        {
            throw new InvalidOperationException("IsConnected property not found on component");
        }

        var isConnected = (bool)isConnectedProperty.GetValue(component)!;
        if (isConnected)
        {
            return true;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var tcs = new TaskCompletionSource<bool>();

            // Create a suitable event handler
            EventHandler<bool> handler = (sender, connected) =>
            {
                if (connected)
                {
                    tcs.TrySetResult(true);
                }
            };

            // Get the event using reflection
            var eventField = component.GetType().GetField("_circuitStateChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (eventField == null)
            {
                // Try to find the event directly
                var eventInfo = component.GetType().GetEvent("CircuitStateChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (eventInfo == null)
                {
                    throw new InvalidOperationException("CircuitStateChanged event not found on component");
                }

                // Add handler using the event's add method
                var addMethod = eventInfo.AddMethod;
                addMethod!.Invoke(component, [handler]);

                try
                {
                    // Wait for connection or timeout
                    return await tcs.Task.WaitAsync(timeoutCts.Token);
                }
                finally
                {
                    // Remove handler using the event's remove method
                    var removeMethod = eventInfo.RemoveMethod;
                    removeMethod!.Invoke(component, [handler]);
                }
            }

            // Get the current delegate
            var currentDelegate = eventField.GetValue(component) as MulticastDelegate;

            // Add our handler to the delegate chain
            var combinedDelegate = Delegate.Combine(currentDelegate, handler);
            eventField.SetValue(component, combinedDelegate);

            try
            {
                // Wait for connection or timeout
                return await tcs.Task.WaitAsync(timeoutCts.Token);
            }
            finally
            {
                // Remove our handler from the delegate chain
                var removedDelegate = Delegate.Remove(
                    eventField.GetValue(component) as MulticastDelegate, handler);
                eventField.SetValue(component, removedDelegate);
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Executes a function with circuit reconnection resilience.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="component">The component base to extend.</param>
    /// <param name="func">The function to execute with resilience.</param>
    /// <param name="reconnectTimeout">Maximum time to wait for reconnection.</param>
    /// <param name="retryCount">Number of times to retry after reconnection.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The result of the function execution.</returns>
    public static async Task<T> WithCircuitResilienceAsync<T>(
        this DropBearComponentBase component,
        Func<CancellationToken, Task<T>> func,
        TimeSpan reconnectTimeout,
        int retryCount = 3,
        CancellationToken cancellationToken = default)
    {
        // Track operation time for diagnostic purposes
        var operationTimer = Stopwatch.StartNew();
        var attempts = 0;

        while (true)
        {
            attempts++;
            try
            {
                var result = await func(cancellationToken);

                // Log the successful completion time if needed
                // e.g., Debug.WriteLine($"Operation completed in {operationTimer.ElapsedMilliseconds}ms after {attempts} attempts");

                return result;
            }
            catch (JSDisconnectedException) when (attempts <= retryCount)
            {
                // Wait for circuit reconnection
                var reconnected = await component.WaitForCircuitConnectionAsync(
                    reconnectTimeout, cancellationToken);

                if (!reconnected)
                {
                    throw new TimeoutException(
                        $"Circuit reconnection timed out after {reconnectTimeout.TotalSeconds} seconds " +
                        $"(Operation has been running for {operationTimer.ElapsedMilliseconds}ms)");
                }

                // Add jitter to retry delay
                var jitter = Random.Shared.Next(50, 250);
                await Task.Delay((100 * attempts) + jitter, cancellationToken);
            }
        }
    }
}
