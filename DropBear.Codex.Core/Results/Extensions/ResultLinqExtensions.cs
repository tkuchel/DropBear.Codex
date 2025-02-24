#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides LINQ-friendly extension methods for <see cref="Result{T,TError}" /> types,
///     allowing usage of <c>Select</c>, <c>SelectMany</c>, and <c>Where</c> in a LINQ expression style.
/// </summary>
public static class ResultLinqExtensions
{
#pragma warning disable CS1584 // XML comment has syntactically incorrect cref attribute

    #region Single Value Operations

    /// <summary>
    ///     Projects the value of a <see cref="Result{T, TError}" /> into a new form via <paramref name="selector" />.
    ///     (Equivalent to <c>result.Map(selector)</c>.)
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
    ///     Projects each value in <paramref name="result" /> into an intermediate <see cref="Result{TIntermediate, TError}" />
    ///     and then combines the intermediate with the original value to produce a final <typeparamref name="TResult" />.
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
    ///     Filters the value of a <see cref="Result{T, TError}" /> based on <paramref name="predicate" />.
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
    ///     Maps each element of a <see cref="Result{IEnumerable{T}, TError}" /> to a new form, optionally preserving order.
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
    ///     Performs a parallel projection of each element in a <see cref="Result{IEnumerable{T}, TError}" />.
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

    private static TError CreateError<TError>(string message)
        where TError : ResultError
    {
        return (TError)Activator.CreateInstance(typeof(TError), message)!;
    }

    /// <summary>
    ///     Filters elements of a <see cref="Result{IEnumerable{T}, TError}" /> based on <paramref name="predicate" />,
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
    ///     Projects each element of a <see cref="Result{IEnumerable{T}, TError}" /> into
    ///     a flattened collection via <paramref name="selector" />.
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
    ///     Aggregates the elements of a <see cref="Result{IEnumerable{T}, TError}" />
    ///     with a specified <paramref name="func" /> and initial <paramref name="seed" />.
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
    ///     Retrieves the first element of a <see cref="Result{IEnumerable{T}, TError}" /> or returns a failure if none exist.
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

#pragma warning restore CS1584 // XML comment has syntactically incorrect cref attribute
}
