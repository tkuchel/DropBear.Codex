#region

using System.Runtime.CompilerServices;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning.Comparers;

/// <summary>
///     Provides a comparer that compares objects by reference equality.
/// </summary>
public sealed class ReferenceEqualityComparer : EqualityComparer<object>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ReferenceEqualityComparer" /> class.
    /// </summary>
    public ReferenceEqualityComparer()
    {
        // This constructor is intentionally left blank, allowing instantiation of the class.
    }

    /// <summary>
    ///     Determines whether the specified objects are equal by comparing their references.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><c>true</c> if the specified objects are the same instance; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? x, object? y)
    {
        return ReferenceEquals(x, y);
    }

    /// <summary>
    ///     Returns a hash code for the specified object based on its reference.
    /// </summary>
    /// <param name="obj">The object for which to get the hash code.</param>
    /// <returns>A hash code for the object.</returns>
    public override int GetHashCode(object obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}
