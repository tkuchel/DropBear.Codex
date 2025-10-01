#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides LINQ-friendly extension methods for Result types.
///     Optimized for .NET 9 with modern collection handling.
/// </summary>
public static class LinqExtensions
{
    #region Single Value Operations

    /// <summary>
    ///     Projects the value of a Result into a new form via selector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult, TError> Select<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, TResult> selector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);
        return result.Map(selector);
    }

    /// <summary>
    ///     Projects each value in result into an intermediate Result
    ///     and then combines the intermediate with the original value to produce a final result.
    /// </summary>
    public static Result<TResult, TError> SelectMany<T, TIntermediate, TResult, TError>(
        this Result<T, TError> result,
        Func<T, Result<TIntermediate, TError>> intermediateSelector,
        Func<T, TIntermediate, TResult> resultSelector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(intermediateSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        return result.Bind(x =>
            intermediateSelector(x).Map(y => resultSelector(x, y)));
    }

    /// <summary>
    ///     Filters the value of a Result based on a predicate.
    /// </summary>
    public static Result<T, TError> Where<T, TError>(
        this Result<T, TError> result,
        Func<T, bool> predicate,
        Func<T, TError> errorSelector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorSelector);

        return result.Bind(x => predicate(x)
            ? result
            : Result<T, TError>.Failure(errorSelector(x)));
    }

    #endregion

    #region Collection Operations

    /// <summary>
    ///     Maps each element of a Result containing a collection to a new form.
    /// </summary>
    public static Result<IEnumerable<TResult>, TError> Select<T, TResult, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, TResult> selector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);
        return result.Map(items => items.Select(selector));
    }

    /// <summary>
    ///     Performs a parallel projection of each element in a Result containing a collection.
    /// </summary>
    public static Result<IEnumerable<TResult>, TError> SelectParallel<T, TResult, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, TResult> selector,
        ParallelOptions? options = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);

        return result.Bind(items =>
        {
            try
            {
                var query = items.AsParallel();

                if (options is not null)
                {
                    query = query
                        .WithDegreeOfParallelism(options.MaxDegreeOfParallelism)
                        .WithCancellation(options.CancellationToken);
                }

                var results = query.Select(selector).ToList();
                return Result<IEnumerable<TResult>, TError>.Success(results);
            }
            catch (AggregateException ex)
            {
                var error = (TError)Activator.CreateInstance(
                    typeof(TError),
                    $"Parallel processing failed: {ex.InnerException?.Message ?? ex.Message}")!;
                return Result<IEnumerable<TResult>, TError>.Failure(error);
            }
            catch (OperationCanceledException)
            {
                var error = (TError)Activator.CreateInstance(
                    typeof(TError),
                    "Operation was cancelled")!;
                return Result<IEnumerable<TResult>, TError>.Failure(error);
            }
        });
    }

    /// <summary>
    ///     Filters elements of a Result containing a collection based on predicate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<T>, TError> Where<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, bool> predicate)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return result.Map(items => items.Where(predicate));
    }

    /// <summary>
    ///     Projects each element of a Result containing a collection into
    ///     a flattened collection via selector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<TResult>, TError> SelectMany<T, TResult, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, IEnumerable<TResult>> selector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);
        return result.Map(items => items.SelectMany(selector));
    }

    /// <summary>
    ///     Orders elements in a Result containing a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<T>, TError> OrderBy<T, TKey, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, TKey> keySelector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return result.Map<IEnumerable<T>>(items => items.OrderBy(keySelector));
    }

    /// <summary>
    ///     Orders elements in a Result containing a collection in descending order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<T>, TError> OrderByDescending<T, TKey, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, TKey> keySelector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return result.Map<IEnumerable<T>>(items => items.OrderByDescending(keySelector));
    }

    /// <summary>
    ///     Groups elements in a Result containing a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<IGrouping<TKey, T>>, TError> GroupBy<T, TKey, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, TKey> keySelector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        return result.Map(items => items.GroupBy(keySelector));
    }

    /// <summary>
    ///     Takes a specified number of elements from a Result containing a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<T>, TError> Take<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        int count)
        where TError : ResultError
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return result.Map(items => items.Take(count));
    }

    /// <summary>
    ///     Skips a specified number of elements in a Result containing a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<T>, TError> Skip<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        int count)
        where TError : ResultError
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return result.Map(items => items.Skip(count));
    }

    /// <summary>
    ///     Returns distinct elements from a Result containing a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<T>, TError> Distinct<T, TError>(
        this Result<IEnumerable<T>, TError> result)
        where TError : ResultError
    {
        return result.Map(items => items.Distinct());
    }

    /// <summary>
    ///     Returns distinct elements using a custom comparer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<T>, TError> Distinct<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        IEqualityComparer<T> comparer)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(comparer);
        return result.Map(items => items.Distinct(comparer));
    }

    #endregion

    #region Aggregation Operations

    /// <summary>
    ///     Aggregates the elements of a Result containing a collection
    ///     with a specified func and initial seed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TAccumulate, TError> Aggregate<T, TAccumulate, TError>(
        this Result<IEnumerable<T>, TError> result,
        TAccumulate seed,
        Func<TAccumulate, T, TAccumulate> func)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(func);
        return result.Map(items => items.Aggregate(seed, func));
    }

    /// <summary>
    ///     Retrieves the first element of a Result containing a collection or returns a failure if none exist.
    /// </summary>
    public static Result<T, TError> FirstOrFailure<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<TError> errorSelector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(errorSelector);

        return result.Bind(items =>
        {
            using var enumerator = items.GetEnumerator();
            return enumerator.MoveNext()
                ? Result<T, TError>.Success(enumerator.Current)
                : Result<T, TError>.Failure(errorSelector());
        });
    }

    /// <summary>
    ///     Retrieves the first element matching a predicate or returns a failure.
    /// </summary>
    public static Result<T, TError> FirstOrFailure<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, bool> predicate,
        Func<TError> errorSelector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorSelector);

        return result.Bind(items =>
        {
            foreach (var item in items)
            {
                if (predicate(item))
                {
                    return Result<T, TError>.Success(item);
                }
            }

            return Result<T, TError>.Failure(errorSelector());
        });
    }

    /// <summary>
    ///     Retrieves the single element of a Result containing a collection or returns a failure.
    /// </summary>
    public static Result<T, TError> SingleOrFailure<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<TError> errorSelector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(errorSelector);

        return result.Bind(items =>
        {
            using var enumerator = items.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return Result<T, TError>.Failure(errorSelector());
            }

            var first = enumerator.Current;
            if (enumerator.MoveNext())
            {
                return Result<T, TError>.Failure(errorSelector());
            }

            return Result<T, TError>.Success(first);
        });
    }

    /// <summary>
    ///     Counts elements in a Result containing a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<int, TError> Count<T, TError>(
        this Result<IEnumerable<T>, TError> result)
        where TError : ResultError
    {
        return result.Map(items => items.Count());
    }

    /// <summary>
    ///     Checks if any elements in a Result containing a collection match a predicate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<bool, TError> Any<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, bool>? predicate = null)
        where TError : ResultError
    {
        return result.Map(items => predicate == null ? items.Any() : items.Any(predicate));
    }

    /// <summary>
    ///     Checks if all elements in a Result containing a collection match a predicate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<bool, TError> All<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, bool> predicate)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return result.Map(items => items.All(predicate));
    }

    /// <summary>
    ///     Computes the sum of numeric values in a Result containing a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<int, TError> Sum<TError>(
        this Result<IEnumerable<int>, TError> result)
        where TError : ResultError
    {
        return result.Map(items => items.Sum());
    }

    /// <summary>
    ///     Computes the sum using a selector function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<int, TError> Sum<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, int> selector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);
        return result.Map(items => items.Sum(selector));
    }

    /// <summary>
    ///     Computes the average of numeric values in a Result containing a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<double, TError> Average<TError>(
        this Result<IEnumerable<int>, TError> result)
        where TError : ResultError
    {
        return result.Map(items => items.Average());
    }

    /// <summary>
    ///     Computes the average using a selector function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<double, TError> Average<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, int> selector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);
        return result.Map(items => items.Average(selector));
    }

    /// <summary>
    ///     Finds the minimum value in a Result containing a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Min<T, TError>(
        this Result<IEnumerable<T>, TError> result)
        where TError : ResultError
    {
        return result.Map(items => items.Min()!);
    }

    /// <summary>
    ///     Finds the maximum value in a Result containing a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Max<T, TError>(
        this Result<IEnumerable<T>, TError> result)
        where TError : ResultError
    {
        return result.Map(items => items.Max()!);
    }

    #endregion

    #region Conversion Operations

    /// <summary>
    ///     Converts a Result containing a collection to a List.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<List<T>, TError> ToList<T, TError>(
        this Result<IEnumerable<T>, TError> result)
        where TError : ResultError
    {
        return result.Map(items => items.ToList());
    }

    /// <summary>
    ///     Converts a Result containing a collection to an Array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T[], TError> ToArray<T, TError>(
        this Result<IEnumerable<T>, TError> result)
        where TError : ResultError
    {
        return result.Map(items => items.ToArray());
    }

    /// <summary>
    ///     Converts a Result containing a collection to a HashSet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<HashSet<T>, TError> ToHashSet<T, TError>(
        this Result<IEnumerable<T>, TError> result)
        where TError : ResultError
    {
        return result.Map(items => items.ToHashSet());
    }

    /// <summary>
    ///     Converts a Result containing a collection to a Dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Dictionary<TKey, TValue>, TError> ToDictionary<T, TKey, TValue, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, TKey> keySelector,
        Func<T, TValue> valueSelector)
        where TKey : notnull
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(valueSelector);
        return result.Map(items => items.ToDictionary(keySelector, valueSelector));
    }

    #endregion

    #region Join Operations

    /// <summary>
    ///     Joins two Result collections based on matching keys.
    /// </summary>
    public static Result<IEnumerable<TResult>, TError> Join<TOuter, TInner, TKey, TResult, TError>(
        this Result<IEnumerable<TOuter>, TError> outer,
        Result<IEnumerable<TInner>, TError> inner,
        Func<TOuter, TKey> outerKeySelector,
        Func<TInner, TKey> innerKeySelector,
        Func<TOuter, TInner, TResult> resultSelector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        return outer.Bind(outerItems =>
            inner.Map(innerItems =>
                outerItems.Join(
                    innerItems,
                    outerKeySelector,
                    innerKeySelector,
                    resultSelector)));
    }

    /// <summary>
    ///     Performs a left outer join on two Result collections.
    /// </summary>
    public static Result<IEnumerable<TResult>, TError> GroupJoin<TOuter, TInner, TKey, TResult, TError>(
        this Result<IEnumerable<TOuter>, TError> outer,
        Result<IEnumerable<TInner>, TError> inner,
        Func<TOuter, TKey> outerKeySelector,
        Func<TInner, TKey> innerKeySelector,
        Func<TOuter, IEnumerable<TInner>, TResult> resultSelector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);

        return outer.Bind(outerItems =>
            inner.Map(innerItems =>
                outerItems.GroupJoin(
                    innerItems,
                    outerKeySelector,
                    innerKeySelector,
                    resultSelector)));
    }

    #endregion

    #region Partitioning Operations

    /// <summary>
    ///     Splits a Result containing a collection into chunks of specified size.
    /// </summary>
    public static Result<IEnumerable<IEnumerable<T>>, TError> Chunk<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        int size)
        where TError : ResultError
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
        return result.Map(items => items.Chunk(size).Select(chunk => chunk.AsEnumerable()));
    }

    /// <summary>
    ///     Takes elements while a condition is true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<T>, TError> TakeWhile<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, bool> predicate)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return result.Map(items => items.TakeWhile(predicate));
    }

    /// <summary>
    ///     Skips elements while a condition is true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<IEnumerable<T>, TError> SkipWhile<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, bool> predicate)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return result.Map(items => items.SkipWhile(predicate));
    }

    #endregion
}
