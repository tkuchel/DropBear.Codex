#region

using System.Collections;
using System.Reflection;
using DropBear.Codex.Utilities.Interfaces;

#endregion

namespace DropBear.Codex.Utilities.Comparers.ComparisonStrategies;

public sealed class CustomObjectComparisonStrategy : IComparisonStrategy
{
    public bool CanCompare(Type type)
    {
        return type is { IsPrimitive: false, IsEnum: false } && type != typeof(string) &&
               !typeof(IEnumerable).IsAssignableFrom(type);
    }

    public double Compare(object? value1, object? value2)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        var properties = value1.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        double totalScore = 0;
        var propertiesCompared = 0;

        foreach (var prop in properties)
        {
            var propValue1 = prop.GetValue(value1);
            var propValue2 = prop.GetValue(value2);

            totalScore += ObjectComparer.CompareValues(propValue1, propValue2);
            propertiesCompared++;
        }

        return propertiesCompared > 0 ? totalScore / propertiesCompared : 0;
    }
}
