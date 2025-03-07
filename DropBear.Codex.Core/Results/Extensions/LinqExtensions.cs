#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides LINQ-friendly extension methods for Result types.
///     These allow for familiar query expressions and transformations.
/// </summary>
public static class LinqExtensions
{
    #region Helper Methods

    /// <summary>
    ///     Creates an error of type TError with the specified message.
    /// </summary>
    private static TError CreateError<TError>(string message)
        where TError : ResultError
    {
        return (TError)Activator.CreateInstance(typeof(TError), message)!;
    }

    #endregion

    #region Single Value Operations

    /// <summary>
    ///     Projects the value of a Result into a new form via selector.
    ///     (Equivalent to result.Map(selector).)
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
    ///     Projects each value in result into an intermediate Result
    ///     and then combines the intermediate with the original value to produce a final result.
    ///     This enables LINQ query expressions with multiple from clauses.
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
    ///     If the predicate fails, returns a new failure result.
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
    ///     Maps each element of a Result containing a collection to a new form,
    ///     optionally preserving the original order.
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
                var query = items.AsParallel().WithMergeOptions(ParallelMergeOptions.NotBuffered);

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

    /// <summary>
    ///     Filters elements of a Result containing a collection based on predicate,
    ///     optionally preserving the original order.
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
    ///     Projects each element of a Result containing a collection into
    ///     a flattened collection via selector.
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
    ///     Aggregates the elements of a Result containing a collection
    ///     with a specified func and initial seed.
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

    #endregion
}
