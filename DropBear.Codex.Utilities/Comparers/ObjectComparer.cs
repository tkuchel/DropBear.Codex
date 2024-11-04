#region

using System.Reflection;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Utilities.Comparers.ComparisonStrategies;
using DropBear.Codex.Utilities.Comparers.Models;
using DropBear.Codex.Utilities.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Comparers;

public class ObjectComparer
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ObjectComparer>();

    private static readonly List<IComparisonStrategy> Strategies =
    [
        new BasicTypeComparisonStrategy(),
        new DictionaryComparisonStrategy(),
        new CustomObjectComparisonStrategy()];

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

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var value1 = prop.GetValue(obj1);
                var value2 = prop.GetValue(obj2);

                var fieldScore = CompareValues(value1, value2);
                compareResult.FieldResults.Add(new FieldCompareResult(prop.Name, fieldScore));
                totalScore += fieldScore;
                propertiesCompared++;
            }

            compareResult.OverallConfidence = propertiesCompared > 0 ? totalScore / propertiesCompared : 0;
            return Result<CompareResult>.Success(compareResult);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during comparison");
            return Result<CompareResult>.Failure("An error occurred during comparison.");
        }
    }

    public static double CompareValues(object? value1, object? value2)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        var strategy = FindStrategy(value1.GetType());
        return strategy?.Compare(value1, value2) ?? 0;
    }

    private static IComparisonStrategy? FindStrategy(Type type)
    {
        return Strategies.Find(strategy => strategy.CanCompare(type));
    }
}
