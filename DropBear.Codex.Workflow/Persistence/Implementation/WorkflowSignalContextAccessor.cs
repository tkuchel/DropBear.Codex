using System.Threading;

namespace DropBear.Codex.Workflow.Persistence.Implementation;

internal static class WorkflowSignalContextAccessor
{
    private static readonly AsyncLocal<PendingSignalContext?> CurrentContext = new();

    public static PendingSignalContext? Current => CurrentContext.Value;

    public static IDisposable BeginScope(string signalName, object? payload)
    {
        var previous = CurrentContext.Value;
        CurrentContext.Value = new PendingSignalContext(signalName, payload);
        return new Scope(previous);
    }

    internal sealed record PendingSignalContext(string SignalName, object? Payload);

    private sealed class Scope(PendingSignalContext? previous) : IDisposable
    {
        public void Dispose()
        {
            CurrentContext.Value = previous;
        }
    }
}
