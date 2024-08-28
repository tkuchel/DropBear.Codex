namespace DropBear.Codex.StateManagement.StateSnapshots.Interfaces;

/// <summary>
///     Defines methods for comparing two objects of type <typeparamref name="T" /> and generating a hash code for an
///     object of type <typeparamref name="T" />.
/// </summary>
/// <typeparam name="T">The type of objects to compare.</typeparam>
public interface IStateComparer<in T>
{
    /// <summary>
    ///     Determines whether two objects of type <typeparamref name="T" /> are equal.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><c>true</c> if the specified objects are equal; otherwise, <c>false</c>.</returns>
    bool Equals(T x, T y);

    /// <summary>
    ///     Returns a hash code for the specified object.
    /// </summary>
    /// <param name="obj">The object for which to get a hash code.</param>
    /// <returns>A hash code for the specified object.</returns>
    int GetHashCode(T obj);
}
