#region

using System.Collections;
using DropBear.Codex.Utilities.Interfaces;

#endregion

namespace DropBear.Codex.Utilities.Comparers.ComparisonStrategies;

/// <summary>
///     A comparison strategy for types implementing <see cref="IDictionary" />.
///     Compares dictionary entries key-by-key using <see cref="ObjectComparer.CompareValues" />.
/// </summary>
public sealed class DictionaryComparisonStrategy : IComparisonStrategy
{
    /// <inheritdoc />
    public bool CanCompare(Type type)
    {
        return typeof(IDictionary).IsAssignableFrom(type);
    }

    /// <inheritdoc />
    public double Compare(object? value1, object? value2)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        var dict1 = (IDictionary)value1;
        var dict2 = (IDictionary)value2;

        // If the dictionary sizes differ, return 0 immediately
        if (dict1.Count != dict2.Count)
        {
            return 0;
        }

        double totalScore = 0;
        var itemCount = 0;

        // For each key in dict1, check existence in dict2 and compare values
        foreach (var key in dict1.Keys)
        {
            if (!dict2.Contains(key))
            {
                return 0;
            }

            var value1Item = dict1[key];
            var value2Item = dict2[key];

            totalScore += ObjectComparer.CompareValues(value1Item, value2Item);
            itemCount++;
        }

        return itemCount > 0 ? totalScore / itemCount : 0;
    }
}
