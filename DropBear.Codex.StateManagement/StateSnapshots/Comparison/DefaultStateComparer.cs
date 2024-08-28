#region

using DropBear.Codex.StateManagement.StateSnapshots.Interfaces;
using Newtonsoft.Json;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots.Models;

/// <summary>
///     Provides a default implementation of <see cref="IStateComparer{T}" /> that compares state objects by serializing
///     them to JSON.
/// </summary>
/// <typeparam name="T">The type of the objects to compare.</typeparam>
public class DefaultStateComparer<T> : IStateComparer<T>
{
    /// <summary>
    ///     Determines whether the specified objects are equal by comparing their JSON representations.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><c>true</c> if the specified objects are equal; otherwise, <c>false</c>.</returns>
    public bool Equals(T x, T y)
    {
        // Handles nulls and serialization to ensure consistent comparison
        return JsonConvert.SerializeObject(x) == JsonConvert.SerializeObject(y);
    }

    /// <summary>
    ///     Returns a hash code for the specified object, based on its JSON representation.
    /// </summary>
    /// <param name="obj">The object for which to get a hash code.</param>
    /// <returns>A hash code for the specified object.</returns>
    public int GetHashCode(T obj)
    {
        // Use ordinal case-insensitive comparison for consistency in hash code generation
        return JsonConvert.SerializeObject(obj).GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}
