#region

using System.Globalization;
using System.Runtime.CompilerServices;
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
    // Constants for optimized string comparison thresholds
    private const int ShortStringLength = 50;
    private const double MinLengthRatio = 0.5;
    private const double NearZeroThreshold = 0.001;

    /// <inheritdoc />
    public bool CanCompare(Type type)
    {
        return type.IsPrimitive || type.IsEnum || type == typeof(string) ||
               type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) || type == typeof(decimal);
    }

    /// <inheritdoc />
    public double Compare(object? value1, object? value2, int currentDepth = 0, int maxDepth = 10)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        // Fast direct reference equality check
        if (ReferenceEquals(value1, value2))
        {
            return 1.0;
        }

        return value1 switch
        {
            // Special handling for common types for better performance
            string str1 when value2 is string str2 => CompareStrings(str1, str2),
            int i1 when value2 is int i2 => CompareIntegers(i1, i2),
            double d1 when value2 is double d2 => CompareDoubles(d1, d2),
            decimal m1 when value2 is decimal m2 => CompareDecimals(m1, m2),
            DateTime dt1 when value2 is DateTime dt2 => CompareDateTimes(dt1, dt2),
            Guid g1 when value2 is Guid g2 => g1.Equals(g2) ? 1.0 : 0.0,
            bool b1 when value2 is bool b2 => b1 == b2 ? 1.0 : 0.0,
            Enum e1 when value2 is Enum e2 => e1.Equals(e2) ? 1.0 : 0.0,
            IComparable comparable1 => CompareNumeric(comparable1, value2),
            _ => value1.Equals(value2) ? 1.0 : 0.0
        };
    }

    /// <summary>
    ///     Optimized string comparison that uses different strategies based on string length.
    /// </summary>
    /// <param name="str1">First string to compare</param>
    /// <param name="str2">Second string to compare</param>
    /// <returns>Confidence score from 0.0 to 1.0</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CompareStrings(string str1, string str2)
    {
        // Fast path for exact matches
        if (string.Equals(str1, str2, StringComparison.Ordinal))
        {
            return 1.0;
        }

        // Fast path for empty strings
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
        {
            return 0.0;
        }

        // Check length ratio for early exit on very different strings
        var lengthRatio = (double)Math.Min(str1.Length, str2.Length) / Math.Max(str1.Length, str2.Length);
        if (lengthRatio < MinLengthRatio)
        {
            return 0.0;
        }

        // For shorter strings, use Levenshtein distance for more precise comparison
        if (str1.Length < ShortStringLength && str2.Length < ShortStringLength)
        {
            var distance = LevenshteinDistance(str1.AsSpan(), str2.AsSpan());
            var maxLength = Math.Max(str1.Length, str2.Length);
            return 1.0 - ((double)distance / maxLength);
        }

        // For longer strings, use Fuzz ratio from FuzzySharp (a port of Python's fuzzywuzzy)
        return Fuzz.Ratio(str1, str2) / 100.0;
    }

    /// <summary>
    ///     Compares integer values by calculating the relative difference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CompareIntegers(int i1, int i2)
    {
        if (i1 == i2)
        {
            return 1.0;
        }

        // Handle special case with zeros to avoid divide-by-zero
        if (i1 == 0 || i2 == 0)
        {
            return 0.0;
        }

        // Compute difference relative to the magnitude of values
        var difference = Math.Abs(i1 - i2);
        var average = Math.Abs((double)(i1 + i2) / 2);

        // Limit result to range [0..1]
        return Math.Max(0.0, 1.0 - (difference / average));
    }

    /// <summary>
    ///     Compares double values with special handling for near-zero values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CompareDoubles(double d1, double d2)
    {
        // Direct equality check for efficiency
        if (Math.Abs(d1 - d2) < double.Epsilon)
        {
            return 1.0;
        }

        // Special handling for near-zero values
        if (Math.Abs(d1) < NearZeroThreshold && Math.Abs(d2) < NearZeroThreshold)
        {
            var absoluteDiff = Math.Abs(d1 - d2);
            return absoluteDiff < NearZeroThreshold ? 1.0 : 0.0;
        }

        // Compute difference relative to the magnitude of values
        var difference = Math.Abs(d1 - d2);
        var average = Math.Abs((d1 + d2) / 2.0);

        // Limit result to range [0..1]
        return Math.Max(0.0, 1.0 - (difference / average));
    }

    /// <summary>
    ///     Compares decimal values with high precision.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CompareDecimals(decimal m1, decimal m2)
    {
        if (m1 == m2)
        {
            return 1.0;
        }

        // Handle special case with zeros
        if (m1 == 0m || m2 == 0m)
        {
            var absoluteDiff = Math.Abs(m1 - m2);
            return absoluteDiff < 0.001m ? 1.0 : 0.0;
        }

        // Compute difference relative to the magnitude of values
        var difference = Math.Abs(m1 - m2);
        var average = Math.Abs((m1 + m2) / 2m);

        // Convert to double for final calculation and limit to range [0..1]
        return Math.Max(0.0, 1.0 - (double)(difference / average));
    }

    /// <summary>
    ///     Compares DateTime values with special handling for different precisions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double CompareDateTimes(DateTime dt1, DateTime dt2)
    {
        // Exact match
        if (dt1 == dt2)
        {
            return 1.0;
        }

        // Calculate time difference
        var difference = Math.Abs((dt1 - dt2).TotalSeconds);

        // Different scales of similarity based on time difference
        if (difference < 1.0)
        {
            return 0.95; // Within a second
        }

        if (difference < 60.0)
        {
            return 0.9; // Within a minute
        }

        if (difference < 3600.0)
        {
            return 0.8; // Within an hour
        }

        if (difference < 86400.0)
        {
            return 0.6; // Within a day
        }

        if (difference < 604800.0)
        {
            return 0.4; // Within a week
        }

        if (difference < 2592000.0)
        {
            return 0.2; // Within a month
        }

        return 0.0; // More than a month apart
    }

    /// <summary>
    ///     Generic comparison for IComparable types.
    /// </summary>
    private double CompareNumeric(IComparable comparable1, object value2)
    {
        // Direct equality check
        if (comparable1.Equals(value2))
        {
            return 1.0;
        }

        try
        {
            // Try to get numeric values for comparison
            var d1 = Convert.ToDouble(comparable1, CultureInfo.InvariantCulture);
            var d2 = Convert.ToDouble(value2, CultureInfo.InvariantCulture);

            // Special handling for near-zero values
            if (Math.Abs(d1) < NearZeroThreshold && Math.Abs(d2) < NearZeroThreshold)
            {
                return Math.Abs(d1 - d2) < NearZeroThreshold ? 1.0 : 0.0;
            }

            // Compute difference relative to the magnitude of values
            var difference = Math.Abs(d1 - d2);
            var average = Math.Abs((d1 + d2) / 2.0);

            // Handle potential divide-by-zero
            if (average < NearZeroThreshold)
            {
                return difference < NearZeroThreshold ? 1.0 : 0.0;
            }

            // Return similarity score
            return Math.Max(0.0, 1.0 - (difference / average));
        }
        catch
        {
            // If numeric conversion fails, fall back to binary comparison
            return 0.0;
        }
    }

    /// <summary>
    ///     Highly optimized Levenshtein distance calculation using spans to minimize allocations.
    /// </summary>
    private static int LevenshteinDistance(ReadOnlySpan<char> s, ReadOnlySpan<char> t)
    {
        var m = s.Length;
        var n = t.Length;

        if (m == 0)
        {
            return n;
        }

        if (n == 0)
        {
            return m;
        }

        // Use stack allocation for small strings, heap for larger ones
        var previousRow = m <= 128 ? stackalloc int[m + 1] : new int[m + 1];
        var currentRow = m <= 128 ? stackalloc int[m + 1] : new int[m + 1];

        // Initialize the first row
        for (var i = 0; i <= m; i++)
        {
            previousRow[i] = i;
        }

        // Fill in the rest of the matrix
        for (var j = 1; j <= n; j++)
        {
            currentRow[0] = j;

            for (var i = 1; i <= m; i++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;

                currentRow[i] = Math.Min(
                    Math.Min(
                        currentRow[i - 1] + 1, // Insertion
                        previousRow[i] + 1), // Deletion
                    previousRow[i - 1] + cost); // Substitution
            }

            // Swap rows for next iteration
            var temp = previousRow;
            previousRow = currentRow;
            currentRow = temp;
        }

        // Return the last element from the last filled row
        return previousRow[m];
    }
}
