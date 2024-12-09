#region

using Microsoft.AspNetCore.Components.Server.Circuits;

#endregion

namespace DropBear.Codex.Blazor.Components.Bases;

/// <summary>
///     Handles Blazor circuit lifecycle events for a component.
/// </summary>
public sealed class ComponentCircuitHandler : CircuitHandler
{
    private readonly DropBearComponentBase _component;

    public ComponentCircuitHandler(DropBearComponentBase component)
    {
        _component = component ?? throw new ArgumentNullException(nameof(component));
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _component.IsConnected = true;
        _component.Logger.Debug("Circuit opened in {ComponentName}", _component.GetType().Name);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _component.IsConnected = false;
        _component.CircuitCts.Cancel();
        _component.Logger.Debug("Circuit closed in {ComponentName}", _component.GetType().Name);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _component.IsConnected = true;
        _component.Logger.Debug("Connection up in {ComponentName}", _component.GetType().Name);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _component.IsConnected = false;
        _component.Logger.Debug("Connection down in {ComponentName}", _component.GetType().Name);
        return Task.CompletedTask;
    }
}
