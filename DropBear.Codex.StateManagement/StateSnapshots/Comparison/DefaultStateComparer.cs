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
///     The type of objects to compare. Objects are compared via their JSON representations.
/// </typeparam>
public class DefaultStateComparer<T> : IStateComparer<T>
{
    /// <inheritdoc />
    public bool Equals(T x, T y)
    {
        var xJson = JsonConvert.SerializeObject(x);
        var yJson = JsonConvert.SerializeObject(y);

        return string.Equals(xJson, yJson, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public int GetHashCode(T obj)
    {
        var json = JsonConvert.SerializeObject(obj);
        return json.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}
