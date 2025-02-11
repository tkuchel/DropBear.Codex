#region

using DropBear.Codex.Core.Results.Compatibility;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots.Interfaces;

public interface ISimpleSnapshotManager<T> where T : ICloneable<T>
{
    Result SaveState(T state);
    Result RestoreState(int version);
    Result<bool> IsDirty(T currentState);
    Result<T?> GetCurrentState();
}
