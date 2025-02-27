namespace DropBear.Codex.Utilities.Interfaces;

/// <summary>
///     Defines the interface for a comparison strategy that compares objects of a specific type.
/// </summary>
public interface IComparisonStrategy
{
    /// <summary>
    ///     Determines whether this strategy can compare objects of the specified type.
    /// </summary>
    /// <param name="type">The runtime type to check.</param>
    /// <returns><c>true</c> if this strategy can compare the specified type; otherwise, <c>false</c>.</returns>
    bool CanCompare(Type type);

    /// <summary>
    ///     Compares two values using this strategy.
    /// </summary>
    /// <param name="value1">The first value to compare.</param>
    /// <param name="value2">The second value to compare.</param>
    /// <param name="currentDepth">Current recursive depth of comparison for complex objects.</param>
    /// <param name="maxDepth">Maximum allowed depth for recursive comparisons.</param>
    /// <returns>
    ///     A confidence score in range [0..1], where 1 means identical or equivalent,
    ///     and 0 means no similarity.
    /// </returns>
    double Compare(object? value1, object? value2, int currentDepth = 0, int maxDepth = 10);
}
