#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.StateManagement.Errors;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots.Interfaces;

/// <summary>
///     Interface for managing state snapshots with Result-based error handling.
/// </summary>
/// <typeparam name="T">The type of state to manage, must be cloneable.</typeparam>
public interface ISimpleSnapshotManager<T> where T : ICloneable<T>
{
    /// <summary>
    ///     Saves the current state as a snapshot.
    /// </summary>
    /// <param name="state">The state to save.</param>
    /// <returns>A result indicating success or failure.</returns>
    Result<Unit, SnapshotError> SaveState(T state);

    /// <summary>
    ///     Restores a previously saved snapshot by version.
    /// </summary>
    /// <param name="version">The version of the snapshot to restore.</param>
    /// <returns>A result indicating success or failure.</returns>
    Result<Unit, SnapshotError> RestoreState(int version);

    /// <summary>
    ///     Determines if the current state is different from the given state.
    /// </summary>
    /// <param name="currentState">The state to compare against.</param>
    /// <returns>A result containing true if the state is dirty, false otherwise.</returns>
    Result<bool, SnapshotError> IsDirty(T currentState);

    /// <summary>
    ///     Gets the current state.
    /// </summary>
    /// <returns>A result containing the current state, or an error if no state is available.</returns>
    Result<T?, SnapshotError> GetCurrentState();
}
