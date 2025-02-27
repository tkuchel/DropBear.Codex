#region

using DropBear.Codex.Utilities.Interfaces;

#endregion

namespace DropBear.Codex.Utilities.Comparers.ComparisonStrategies;

/// <summary>
///     A comparison strategy that always returns 0 confidence (no match).
///     Used as a fallback when no other strategy applies to a given type.
/// </summary>
public sealed class NullComparisonStrategy : IComparisonStrategy
{
    /// <summary>
    ///     Gets the singleton instance of <see cref="NullComparisonStrategy" />.
    /// </summary>
    public static readonly NullComparisonStrategy Instance = new();

    // Private constructor enforces singleton pattern
    private NullComparisonStrategy() { }

    /// <inheritdoc />
    public bool CanCompare(Type type)
    {
        return true;
    }

    /// <inheritdoc />
    public double Compare(object? value1, object? value2, int currentDepth = 0, int maxDepth = 10)
    {
        return 0.0;
    }
}
