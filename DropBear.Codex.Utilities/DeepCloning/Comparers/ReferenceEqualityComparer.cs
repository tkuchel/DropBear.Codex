#region

using System.Runtime.CompilerServices;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning.Comparers;

/// <summary>
///     Provides a comparer that compares objects by reference equality.
///     This is useful for tracking object instances during cloning to handle circular references.
/// </summary>
public sealed class ReferenceEqualityComparer : EqualityComparer<object>
{
    /// <summary>
    ///     The singleton instance of the <see cref="ReferenceEqualityComparer" /> class.
    /// </summary>
    public static readonly ReferenceEqualityComparer Instance = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReferenceEqualityComparer" /> class.
    /// </summary>
    /// <remarks>
    ///     Consider using the <see cref="Instance" /> singleton instead of creating new instances.
    /// </remarks>
    public ReferenceEqualityComparer()
    {
        // This constructor is intentionally left public for backward compatibility
    }

    /// <summary>
    ///     Determines whether the specified objects are equal by comparing their references.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><c>true</c> if the specified objects are the same instance; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? x, object? y)
    {
        return ReferenceEquals(x, y);
    }

    /// <summary>
    ///     Returns a hash code for the specified object based on its reference.
    /// </summary>
    /// <param name="obj">The object for which to get the hash code.</param>
    /// <returns>A hash code for the object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="obj" /> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode(object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    ///     Creates a new dictionary that uses reference equality for comparing keys.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in the dictionary.</typeparam>
    /// <param name="capacity">The initial capacity of the dictionary.</param>
    /// <returns>A new dictionary that uses reference equality for keys.</returns>
    public static IDictionary<object, TValue> CreateDictionary<TValue>(int capacity = 0)
    {
        return capacity > 0
            ? new Dictionary<object, TValue>(capacity, Instance)
            : new Dictionary<object, TValue>(Instance);
    }
}
