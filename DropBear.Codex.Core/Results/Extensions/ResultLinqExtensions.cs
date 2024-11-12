#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides LINQ extension methods for Result types
/// </summary>
public static class ResultLinqExtensions
{
    #region Single Value Operations

    /// <summary>
    ///     Projects a Result value into a new form
    /// </summary>
    public static Result<TResult, TError> Select<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, TResult> selector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);
        return result.Map(selector);
    }

    /// <summary>
    ///     Projects and flattens a Result value using a selector and result selector
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
            intermediateSelector(x).Map(y =>
                resultSelector(x, y)));
    }

    /// <summary>
    ///     Filters a Result value based on a predicate
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
    ///     Projects each element of a Result collection into a new form
    /// </summary>
    public static Result<IEnumerable<TResult>, TError> Select<T, TResult, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, TResult> selector,
        bool preserveOrder = true)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);

        return result.Map(items => preserveOrder
            ? items.Select(selector)
            : items.AsParallel()
                .WithMergeOptions(ParallelMergeOptions.NotBuffered)
                .Select(selector));
    }

    /// <summary>
    ///     Projects each element of a Result collection into a new form with parallel processing
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
                var query = items.AsParallel()
                    .WithMergeOptions(ParallelMergeOptions.NotBuffered);

                if (options is not null)
                {
                    query = query
                        .WithDegreeOfParallelism(options.MaxDegreeOfParallelism)
                        .WithCancellation(options.CancellationToken);
                }

                // Force evaluation and handle any parallel processing exceptions
                var results = query.Select(selector).ToList();
                return Result<IEnumerable<TResult>, TError>.Success(results);
            }
            catch (AggregateException ex)
            {
                // Handle parallel processing exceptions
                return Result<IEnumerable<TResult>, TError>.Failure(
                    CreateError<TError>($"Parallel processing failed: {ex.InnerException?.Message ?? ex.Message}"));
            }
            catch (OperationCanceledException)
            {
                return Result<IEnumerable<TResult>, TError>.Failure(
                    CreateError<TError>("Operation was cancelled"));
            }
        });
    }

    private static TError CreateError<TError>(string message) where TError : ResultError
    {
        return (TError)Activator.CreateInstance(typeof(TError), message)!;
    }

    /// <summary>
    ///     Filters the elements of a Result collection based on a predicate
    /// </summary>
    public static Result<IEnumerable<T>, TError> Where<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, bool> predicate,
        bool preserveOrder = true)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return result.Map(items => preserveOrder
            ? items.Where(predicate)
            : items.AsParallel()
                .WithMergeOptions(ParallelMergeOptions.NotBuffered)
                .Where(predicate));
    }

    /// <summary>
    ///     Projects and flattens each element of a Result collection
    /// </summary>
    public static Result<IEnumerable<TResult>, TError> SelectMany<T, TResult, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, IEnumerable<TResult>> selector,
        bool preserveOrder = true)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);

        return result.Map(items => preserveOrder
            ? items.SelectMany(selector)
            : items.AsParallel()
                .WithMergeOptions(ParallelMergeOptions.NotBuffered)
                .SelectMany(selector));
    }

    #endregion

    #region Aggregation Operations

    /// <summary>
    ///     Aggregates a Result collection using a specified accumulator function
    /// </summary>
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
    ///     Returns the first element of a Result collection, or a failure if the collection is empty
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

    #endregion
}
