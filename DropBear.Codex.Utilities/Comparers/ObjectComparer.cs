#region

using System.Reflection;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Utilities.Comparers.ComparisonStrategies;
using DropBear.Codex.Utilities.Comparers.Models;
using DropBear.Codex.Utilities.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Comparers;

/// <summary>
///     Provides static methods for comparing two objects, using a set of <see cref="IComparisonStrategy" />
///     to handle different data types (e.g., basic types, dictionaries, custom objects).
/// </summary>
public class ObjectComparer
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ObjectComparer>();

    // The order here can matter if multiple strategies could match the same type,
    // so we place them in a best-guess order.
    private static readonly List<IComparisonStrategy> Strategies = new()
    {
        new BasicTypeComparisonStrategy(), new DictionaryComparisonStrategy(), new CustomObjectComparisonStrategy()
    };

    /// <summary>
    ///     Compares two objects of type <typeparamref name="T" /> property by property,
    ///     and returns a <see cref="CompareResult" /> containing overall and field-level confidence scores.
    /// </summary>
    /// <typeparam name="T">The type of objects being compared.</typeparam>
    /// <param name="obj1">The first object to compare.</param>
    /// <param name="obj2">The second object to compare.</param>
    /// <returns>
    ///     A <see cref="Result{CompareResult}" /> containing a <see cref="CompareResult" />
    ///     if comparison succeeds, or an error result if comparison fails.
    /// </returns>
    public static Result<CompareResult> Compare<T>(T obj1, T obj2)
    {
        try
        {
            if (obj1 == null || obj2 == null)
            {
                return Result<CompareResult>.Failure("One or both objects are null.");
            }

            double totalScore = 0;
            var propertiesCompared = 0;

            var compareResult = new CompareResult(0);

            // Use reflection to get public instance properties
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var value1 = prop.GetValue(obj1);
                var value2 = prop.GetValue(obj2);

                var fieldScore = CompareValues(value1, value2);
                compareResult.FieldResults.Add(
                    new FieldCompareResult(prop.Name, fieldScore));

                totalScore += fieldScore;
                propertiesCompared++;
            }

            // OverallConfidence is average of property-level scores
            compareResult.OverallConfidence = propertiesCompared > 0
                ? totalScore / propertiesCompared
                : 0;

            return Result<CompareResult>.Success(compareResult);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during comparison");
            return Result<CompareResult>.Failure("An error occurred during comparison.");
        }
    }

    /// <summary>
    ///     Compares two values of the same runtime type, delegating to an <see cref="IComparisonStrategy" /> if found.
    /// </summary>
    /// <param name="value1">The first value.</param>
    /// <param name="value2">The second value.</param>
    /// <returns>A confidence score in [0..1], where 1 means identical/equivalent, 0 means no similarity.</returns>
    public static double CompareValues(object? value1, object? value2)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        var strategy = FindStrategy(value1.GetType());
        return strategy?.Compare(value1, value2) ?? 0;
    }

    /// <summary>
    ///     Finds a matching <see cref="IComparisonStrategy" /> for the given <paramref name="type" />
    ///     or returns <c>null</c> if none is found.
    /// </summary>
    /// <param name="type">The runtime type to match against known strategies.</param>
    private static IComparisonStrategy? FindStrategy(Type type)
    {
        return Strategies.Find(strategy => strategy.CanCompare(type));
    }
}
