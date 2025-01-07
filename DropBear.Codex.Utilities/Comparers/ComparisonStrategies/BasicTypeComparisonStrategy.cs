#region

using System.Globalization;
using DropBear.Codex.Utilities.Interfaces;
using FuzzySharp;

#endregion

namespace DropBear.Codex.Utilities.Comparers.ComparisonStrategies;

/// <summary>
///     A comparison strategy handling basic types: primitives, strings, and enums.
///     Uses fuzzy string matching for <c>string</c> types, and numeric difference for <c>IComparable</c> types.
/// </summary>
public sealed class BasicTypeComparisonStrategy : IComparisonStrategy
{
    /// <inheritdoc />
    public bool CanCompare(Type type)
    {
        return type.IsPrimitive || type.IsEnum || type == typeof(string);
    }

    /// <inheritdoc />
    public double Compare(object? value1, object? value2)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        switch (value1)
        {
            case string str1 when value2 is string str2:
                // Use fuzz ratio (0..100), then convert to [0..1]
                return Fuzz.Ratio(str1, str2) / 100.0;

            case IComparable comparable1:
                // Attempt numeric difference. For instance, if both are int or double, we compute difference
                var d1 = Convert.ToDouble(comparable1, CultureInfo.CurrentCulture);
                var d2 = Convert.ToDouble(value2, CultureInfo.CurrentCulture);

                var difference = Math.Abs(d1 - d2);
                var average = (d1 + d2) / 2.0;
                if (average == 0)
                {
                    // If both are zero or near-zero, check if they're actually equal
                    return Math.Abs(d1 - d2) < double.Epsilon ? 1.0 : 0.0;
                }

                // As difference grows, similarity shrinks
                return 1.0 - (difference / average);

            default:
                // Fallback to direct equality check
                return value1.Equals(value2) ? 1.0 : 0.0;
        }
    }
}
