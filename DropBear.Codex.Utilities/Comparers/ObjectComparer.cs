#region

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Extensions;
using DropBear.Codex.Utilities.Comparers.ComparisonStrategies;
using DropBear.Codex.Utilities.Comparers.Models;
using DropBear.Codex.Utilities.Errors;
using DropBear.Codex.Utilities.Interfaces;
using Microsoft.Extensions.ObjectPool;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Comparers;

/// <summary>
///     Provides static methods for comparing two objects, using a set of <see cref="IComparisonStrategy" />
///     to handle different data types (e.g., basic types, dictionaries, custom objects).
/// </summary>
public static class ObjectComparer
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForStaticClass(typeof(ObjectComparer));

    // Cache type-to-strategy mappings to improve performance
    private static readonly ConcurrentDictionary<Type, IComparisonStrategy> TypeToStrategyCache = new();

    // Object pooling for CompareResult instances
    private static readonly ObjectPool<CompareResult> CompareResultPool =
        new DefaultObjectPool<CompareResult>(new CompareResultPoolPolicy());

    // Default ordering of strategies - place more specific strategies first
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
    /// <param name="maxDepth">Maximum recursion depth for nested object comparison.</param>
    /// <returns>
    ///     A <see cref="Result{CompareResult, ObjectComparisonError}" /> containing a <see cref="CompareResult" />
    ///     if comparison succeeds, or an error result if comparison fails.
    /// </returns>
    public static Result<CompareResult, ObjectComparisonError> Compare<T>(T obj1, T obj2, int maxDepth = 10)
    {
        var timing = new OperationTiming(DateTime.UtcNow);
        var compareResult = CompareResultPool.Get();

        try
        {
            if (obj1 == null || obj2 == null)
            {
                CompareResultPool.Return(compareResult);
                return Result<CompareResult, ObjectComparisonError>.Failure(
                    new ObjectComparisonError("One or both objects are null."));
            }

            double totalScore = 0;
            var propertiesCompared = 0;

            // Use reflection to get public instance properties
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (!ShouldCompareProperty(prop))
                {
                    continue;
                }

                var value1 = prop.GetValue(obj1);
                var value2 = prop.GetValue(obj2);

                var fieldScore = CompareValues(value1, value2, 0, maxDepth);
                compareResult.FieldResults.Add(
                    new FieldCompareResult(prop.Name, fieldScore));

                totalScore += fieldScore;
                propertiesCompared++;
            }

            // OverallConfidence is average of property-level scores
            compareResult.OverallConfidence = propertiesCompared > 0
                ? totalScore / propertiesCompared
                : 0;

            var result = Result<CompareResult, ObjectComparisonError>.Success(compareResult);

            // Record timing data as metadata
            var endTime = DateTime.UtcNow;
            result = result.WithTiming(timing =>
                Logger.Debug("Object comparison completed in {Duration}ms", timing.Duration.TotalMilliseconds));

            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during comparison");
            CompareResultPool.Return(compareResult);
            return Result<CompareResult, ObjectComparisonError>.Failure(
                new ObjectComparisonError($"An error occurred during comparison: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Compares two objects asynchronously using parallel processing for improved performance
    ///     on complex objects with many properties.
    /// </summary>
    /// <typeparam name="T">The type of objects being compared.</typeparam>
    /// <param name="obj1">The first object to compare.</param>
    /// <param name="obj2">The second object to compare.</param>
    /// <param name="maxConcurrency">Maximum number of parallel tasks.</param>
    /// <param name="maxDepth">Maximum recursion depth.</param>
    /// <returns>
    ///     A Task containing a <see cref="Result{CompareResult, ObjectComparisonError}" /> with comparison results.
    /// </returns>
    public static async Task<Result<CompareResult, ObjectComparisonError>> CompareParallelAsync<T>(
        T obj1, T obj2, int maxConcurrency = 4, int maxDepth = 10)
    {
        var timing = new OperationTiming(DateTime.UtcNow);

        try
        {
            if (obj1 == null || obj2 == null)
            {
                return Result<CompareResult, ObjectComparisonError>.Failure(
                    new ObjectComparisonError("One or both objects are null."));
            }

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(ShouldCompareProperty)
                .ToArray();

            if (properties.Length == 0)
            {
                var emptyResult = new CompareResult(0);
                return Result<CompareResult, ObjectComparisonError>.Success(emptyResult);
            }

            var fieldResults = new ConcurrentBag<FieldCompareResult>();

            // Process properties in parallel
            await Parallel.ForEachAsync(
                properties,
                new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency },
                async (prop, ct) =>
                {
                    var value1 = prop.GetValue(obj1);
                    var value2 = prop.GetValue(obj2);

                    // Use ValueTask to avoid thread pool starvation for CPU-bound work
                    var fieldScore = await ValueTask.FromResult(CompareValues(value1, value2, 0, maxDepth)).ConfigureAwait(false);
                    fieldResults.Add(new FieldCompareResult(prop.Name, fieldScore));
                }).ConfigureAwait(false);

            var resultList = fieldResults.ToList();
            var totalScore = resultList.Sum(f => f.Confidence);
            var propertiesCompared = resultList.Count;

            var compareResult = new CompareResult(
                propertiesCompared > 0 ? totalScore / propertiesCompared : 0,
                resultList);

            var result = Result<CompareResult, ObjectComparisonError>.Success(compareResult);

            // Add timing information
            var endTime = DateTime.UtcNow;
            Logger.Debug("Parallel object comparison completed in {Duration}ms",
                (endTime - timing.StartTime).TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during parallel comparison");
            return Result<CompareResult, ObjectComparisonError>.Failure(
                new ObjectComparisonError($"An error occurred during parallel comparison: {ex.Message}"),
                ex);
        }
    }

    /// <summary>
    ///     Determines if a property should be included in comparison operations.
    /// </summary>
    /// <param name="property">The property to check.</param>
    /// <returns>True if the property should be compared; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldCompareProperty(PropertyInfo property)
    {
        return property.CanRead && !property.GetIndexParameters().Any();
    }

    /// <summary>
    ///     Compares two values of potentially the same runtime type, delegating to an <see cref="IComparisonStrategy" /> if
    ///     found.
    /// </summary>
    /// <param name="value1">The first value.</param>
    /// <param name="value2">The second value.</param>
    /// <param name="currentDepth">Current recursion depth.</param>
    /// <param name="maxDepth">Maximum recursion depth.</param>
    /// <returns>A confidence score in [0..1], where 1 means identical/equivalent, 0 means no similarity.</returns>
    public static double CompareValues(object? value1, object? value2, int currentDepth = 0, int maxDepth = 10)
    {
        // Handle null values
        if (value1 == null && value2 == null)
        {
            return 1.0; // Both null = perfect match
        }

        if (value1 == null || value2 == null)
        {
            return 0.0; // One null, one not = no match
        }

        if (value1.GetType() != value2.GetType())
        {
            return 0.0; // Different types = no match
        }

        // Prevent stack overflow with deep object hierarchies
        if (currentDepth >= maxDepth)
        {
            Logger.Debug("Maximum comparison depth reached ({MaxDepth})", maxDepth);
            return 0.5; // Return moderate confidence when max depth is reached
        }

        // Find appropriate strategy
        var type = value1.GetType();
        var strategy = FindStrategy(type);
        if (strategy == null)
        {
            Logger.Debug("No comparison strategy found for type {Type}", type.Name);
            return 0.0;
        }

        try
        {
            return strategy.Compare(value1, value2, currentDepth + 1, maxDepth);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during value comparison for type {Type}", type.Name);
            return 0.0;
        }
    }

    /// <summary>
    ///     Finds a matching <see cref="IComparisonStrategy" /> for the given <paramref name="type" />
    ///     or returns <c>null</c> if none is found.
    /// </summary>
    /// <param name="type">The runtime type to match against known strategies.</param>
    private static IComparisonStrategy? FindStrategy(Type type)
    {
        // Check cache first for performance
        return TypeToStrategyCache.GetOrAdd(type, t =>
        {
            var strategy = Strategies.FirstOrDefault(s => s.CanCompare(t));
            if (strategy == null)
            {
                Logger.Debug("No strategy found for type {Type}", t.Name);
                return NullComparisonStrategy.Instance;
            }

            return strategy;
        });
    }

    // Default pooling policy for CompareResult
    private sealed class CompareResultPoolPolicy : IPooledObjectPolicy<CompareResult>
    {
        public CompareResult Create()
        {
            return new CompareResult(0);
        }

        public bool Return(CompareResult obj)
        {
            // Reset the object for reuse
            obj.OverallConfidence = 0;
            obj.FieldResults.Clear();
            return true;
        }
    }
}
