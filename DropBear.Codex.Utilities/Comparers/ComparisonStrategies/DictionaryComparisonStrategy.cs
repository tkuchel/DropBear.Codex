#region

using System.Collections;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Utilities.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Comparers.ComparisonStrategies;

/// <summary>
///     A comparison strategy for types implementing <see cref="IDictionary" />.
///     Compares dictionary entries key-by-key using <see cref="ObjectComparer.CompareValues" />.
/// </summary>
public sealed class DictionaryComparisonStrategy : IComparisonStrategy
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DictionaryComparisonStrategy>();

    /// <inheritdoc />
    public bool CanCompare(Type type)
    {
        return typeof(IDictionary).IsAssignableFrom(type);
    }

    /// <inheritdoc />
    public double Compare(object? value1, object? value2, int currentDepth = 0, int maxDepth = 10)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        // Fast reference equality check
        if (ReferenceEquals(value1, value2))
        {
            return 1.0;
        }

        // Check recursion depth to prevent stack overflow
        if (currentDepth >= maxDepth)
        {
            Logger.Debug("Maximum recursion depth reached for dictionary comparison");
            return 0.5; // Return medium confidence when max depth is reached
        }

        try
        {
            var dict1 = (IDictionary)value1;
            var dict2 = (IDictionary)value2;

            // If the dictionary sizes differ, return partial similarity based on size ratio
            if (dict1.Count != dict2.Count)
            {
                var sizeRatio = (double)Math.Min(dict1.Count, dict2.Count) / Math.Max(dict1.Count, dict2.Count);
                return sizeRatio * 0.5; // Size difference reduces confidence by at least half
            }

            double totalScore = 0;
            var keysCompared = 0;

            // Create a set of keys from dict2 for faster lookup
            var keys2 = new HashSet<object>(dict2.Keys.Cast<object>());

            // For each key in dict1, check existence in dict2 and compare values
            foreach (var key in dict1.Keys)
            {
                if (!keys2.Contains(key))
                {
                    // Key not found in dict2, skip but reduce overall confidence
                    totalScore += 0;
                    keysCompared++;
                    continue;
                }

                var value1Item = dict1[key];
                var value2Item = dict2[key];

                // Compare the values recursively
                var keyScore = ObjectComparer.CompareValues(
                    value1Item, value2Item, currentDepth + 1, maxDepth);

                totalScore += keyScore;
                keysCompared++;
            }

            return keysCompared > 0 ? totalScore / keysCompared : 0;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error during dictionary comparison");
            return 0;
        }
    }

    /// <summary>
    ///     Optimized key lookup for dictionaries without recreating a HashSet.
    /// </summary>
    /// <param name="dict">The dictionary to search in.</param>
    /// <param name="key">The key to find.</param>
    /// <returns>True if the key exists; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ContainsKey(IDictionary dict, object key)
    {
        // Use the IDictionary's more efficient Contains method directly
        return dict.Contains(key);
    }
}
