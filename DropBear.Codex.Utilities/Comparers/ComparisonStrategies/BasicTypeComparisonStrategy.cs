#region

using System.Globalization;
using DropBear.Codex.Utilities.Interfaces;
using FuzzySharp;

#endregion

namespace DropBear.Codex.Utilities.Comparers.ComparisonStrategies;

public sealed class BasicTypeComparisonStrategy : IComparisonStrategy
{
    public bool CanCompare(Type type)
    {
        return type.IsPrimitive || type == typeof(string) || type.IsEnum;
    }

    public double Compare(object? value1, object? value2)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        switch (value1)
        {
            case string str1 when value2 is string str2:
                return Fuzz.Ratio(str1, str2) / 100.0;
            case IComparable comparable1:
            {
                var difference = Math.Abs(Convert.ToDouble(comparable1, CultureInfo.CurrentCulture) -
                                          Convert.ToDouble(value2, CultureInfo.CurrentCulture));
                var average = (Convert.ToDouble(comparable1, CultureInfo.CurrentCulture) +
                               Convert.ToDouble(value2, CultureInfo.CurrentCulture)) / 2.0;
                return 1.0 - (difference / average);
            }
            default:
                return value1.Equals(value2) ? 1 : 0;
        }
    }
}
