#region

using DropBear.Codex.StateManagement.StateSnapshots.Interfaces;
using Newtonsoft.Json;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots.Comparison;

/// <summary>
///     Provides a default implementation of <see cref="IStateComparer{T}" />
///     that compares state objects by serializing them to JSON.
/// </summary>
/// <typeparam name="T">
///     The type of the objects to compare. Objects are compared via their JSON representations.
/// </typeparam>
public class DefaultStateComparer<T> : IStateComparer<T>
{
    /// <summary>
    ///     Determines whether the specified objects <paramref name="x" /> and <paramref name="y" /> are equal
    ///     by comparing their JSON representations (ignoring case).
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><c>true</c> if the objects are considered equal; otherwise, <c>false</c>.</returns>
    public bool Equals(T x, T y)
    {
        var xJson = JsonConvert.SerializeObject(x);
        var yJson = JsonConvert.SerializeObject(y);

        return string.Equals(xJson, yJson, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Returns a hash code for the specified <paramref name="obj" />,
    ///     derived from its JSON representation (ignoring case).
    /// </summary>
    /// <param name="obj">The object for which to get a hash code.</param>
    /// <returns>A hash code for <paramref name="obj" />, derived from its case-insensitive JSON.</returns>
    public int GetHashCode(T obj)
    {
        var json = JsonConvert.SerializeObject(obj);
        return json.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}
