namespace DropBear.Codex.StateManagement.StateSnapshots.Interfaces;

/// <summary>
///     Defines a mechanism for creating a copy of the current object.
/// </summary>
/// <typeparam name="T">The type of object that is being cloned.</typeparam>
public interface ICloneable<out T>
{
    /// <summary>
    ///     Creates a new object that is a copy of the current instance.
    /// </summary>
    /// <returns>A new object that is a copy of this instance.</returns>
    T Clone();
}
