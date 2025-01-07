#region

using System.Collections;
using System.Reflection;
using DropBear.Codex.Utilities.Interfaces;

#endregion

namespace DropBear.Codex.Utilities.Comparers.ComparisonStrategies;

/// <summary>
///     A comparison strategy for custom object types that are not strings, not enumerations, and not IEnumerable.
///     Compares property-by-property using <see cref="ObjectComparer.CompareValues" />.
/// </summary>
public sealed class CustomObjectComparisonStrategy : IComparisonStrategy
{
    /// <inheritdoc />
    public bool CanCompare(Type type)
    {
        // We exclude:
        // 1) Primitive or enum types
        // 2) Strings
        // 3) Anything that implements IEnumerable
        return !type.IsPrimitive && !type.IsEnum && type != typeof(string) &&
               !typeof(IEnumerable).IsAssignableFrom(type);
    }

    /// <inheritdoc />
    public double Compare(object? value1, object? value2)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        var properties = value1.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        double totalScore = 0;
        var propertiesCompared = 0;

        // Summation of property-level comparisons
        foreach (var prop in properties)
        {
            var propValue1 = prop.GetValue(value1);
            var propValue2 = prop.GetValue(value2);

            totalScore += ObjectComparer.CompareValues(propValue1, propValue2);
            propertiesCompared++;
        }

        return propertiesCompared > 0
            ? totalScore / propertiesCompared
            : 0;
    }
}
