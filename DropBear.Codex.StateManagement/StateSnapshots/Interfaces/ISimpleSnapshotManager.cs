using DropBear.Codex.Core;

namespace DropBear.Codex.StateManagement.StateSnapshots.Interfaces;

public interface ISimpleSnapshotManager<T> where T : ICloneable<T>
{
    Result SaveState(T state);
    Result RestoreState(int version);
    Result<bool> IsDirty(T currentState);
    Result<T?> GetCurrentState();
}
