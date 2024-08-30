#region

using System.Collections;
using DropBear.Codex.Utilities.Interfaces;

#endregion

namespace DropBear.Codex.Utilities.Comparers.ComparisonStrategies;

public sealed class DictionaryComparisonStrategy : IComparisonStrategy
{
    public bool CanCompare(Type type)
    {
        return typeof(IDictionary).IsAssignableFrom(type);
    }

    public double Compare(object? value1, object? value2)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        var dict1 = (IDictionary)value1;
        var dict2 = (IDictionary)value2;

        if (dict1.Count != dict2.Count)
        {
            return 0;
        }

        double totalScore = 0;
        var itemCount = 0;

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
