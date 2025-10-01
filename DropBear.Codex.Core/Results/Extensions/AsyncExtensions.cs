#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Async;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides extension methods for working with asynchronous Result types and enumerables.
///     Optimized for .NET 9 with ValueTask patterns and improved performance.
/// </summary>
public static class AsyncExtensions
{
    #region Async Result Extensions

    /// <summary>
    ///     Asynchronously maps a Result to a new Result with a transformed value.
    /// </summary>
    public static async ValueTask<Result<TResult, TError>> MapAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, CancellationToken, ValueTask<TResult>> mapperAsync,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(mapperAsync);

        if (!result.IsSuccess)
        {
            return Result<TResult, TError>.Failure(result.Error!);
        }

        try
        {
            var mappedValue = await mapperAsync(result.Value!, cancellationToken).ConfigureAwait(false);
            return Result<TResult, TError>.Success(mappedValue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TResult, TError>.Failure(result.Error!, ex);
        }
    }

    /// <summary>
    ///     Asynchronously binds a Result to a new Result using an async function.
    /// </summary>
    public static async ValueTask<Result<TResult, TError>> BindAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, CancellationToken, ValueTask<Result<TResult, TError>>> binderAsync,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(binderAsync);

        if (!result.IsSuccess)
        {
            return Result<TResult, TError>.Failure(result.Error!);
        }

        try
        {
            return await binderAsync(result.Value!, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TResult, TError>.Failure(result.Error!, ex);
        }
    }

    /// <summary>
    ///     Converts a Task returning a Result to a flat Result, handling task-level exceptions.
    /// </summary>
    public static async ValueTask<Result<T, TError>> FlattenAsync<T, TError>(
        this Task<Result<T, TError>> resultTask,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(resultTask);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), "Operation was cancelled")!;
            return Result<T, TError>.Cancelled(errorInstance);
        }
        catch (Exception ex)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), $"Task failed: {ex.Message}")!;
            return Result<T, TError>.Failure(errorInstance, ex);
        }
    }

    /// <summary>
    ///     Converts a ValueTask returning a Result to a flat Result.
    /// </summary>
    public static async ValueTask<Result<T, TError>> FlattenAsync<T, TError>(
        this ValueTask<Result<T, TError>> resultTask,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), "Operation was cancelled")!;
            return Result<T, TError>.Cancelled(errorInstance);
        }
        catch (Exception ex)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), $"ValueTask failed: {ex.Message}")!;
            return Result<T, TError>.Failure(errorInstance, ex);
        }
    }

    #endregion

    #region IAsyncEnumerable Extensions

    /// <summary>
    ///     Converts an IAsyncEnumerable to an AsyncEnumerableResult.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncEnumerableResult<T, TError> ToResult<T, TError>(
        this IAsyncEnumerable<T> enumerable)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        return AsyncEnumerableResult<T, TError>.Success(enumerable);
    }

    #endregion

    #region AsyncEnumerableResult Extensions

    /// <summary>
    ///     Filters items in an AsyncEnumerableResult based on a predicate.
    /// </summary>
    public static AsyncEnumerableResult<T, TError> Where<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, bool> predicate)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (!result.IsSuccess)
        {
            return result;
        }

        async IAsyncEnumerable<T> FilterAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (predicate(item))
                {
                    yield return item;
                }
            }
        }

        return AsyncEnumerableResult<T, TError>.Success(FilterAsync());
    }

    /// <summary>
    ///     Filters items asynchronously in an AsyncEnumerableResult.
    /// </summary>
    public static AsyncEnumerableResult<T, TError> WhereAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, ValueTask<bool>> predicateAsync)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(predicateAsync);

        if (!result.IsSuccess)
        {
            return result;
        }

        async IAsyncEnumerable<T> FilterAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (await predicateAsync(item).ConfigureAwait(false))
                {
                    yield return item;
                }
            }
        }

        return AsyncEnumerableResult<T, TError>.Success(FilterAsync());
    }

    /// <summary>
    ///     Maps items in an AsyncEnumerableResult using a transformation function.
    /// </summary>
    public static AsyncEnumerableResult<TResult, TError> Select<T, TResult, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, TResult> selector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(result.Error!);
        }

        async IAsyncEnumerable<TResult> SelectAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return selector(item);
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(SelectAsync());
    }

    /// <summary>
    ///     Applies an async transformation to each item in an AsyncEnumerableResult.
    /// </summary>
    public static AsyncEnumerableResult<TResult, TError> SelectAsync<T, TResult, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, ValueTask<TResult>> selector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(result.Error!);
        }

        async IAsyncEnumerable<TResult> SelectAsyncEnumerable(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return await selector(item).ConfigureAwait(false);
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(SelectAsyncEnumerable());
    }

    /// <summary>
    ///     Flattens nested async enumerables using SelectMany.
    /// </summary>
    public static AsyncEnumerableResult<TResult, TError> SelectMany<T, TResult, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, IAsyncEnumerable<TResult>> selector)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(result.Error!);
        }

        async IAsyncEnumerable<TResult> SelectManyAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await foreach (var subItem in selector(item).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return subItem;
                }
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(SelectManyAsync());
    }

    /// <summary>
    ///     Converts an AsyncEnumerableResult to a List within a Result.
    /// </summary>
    public static async ValueTask<Result<List<T>, TError>> ToListAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<List<T>, TError>.Failure(result.Error!);
        }

        var list = new List<T>();
        await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            list.Add(item);
        }

        return Result<List<T>, TError>.Success(list);
    }

    /// <summary>
    ///     Converts an AsyncEnumerableResult to an Array within a Result.
    /// </summary>
    public static async ValueTask<Result<T[], TError>> ToArrayAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<T[], TError>.Failure(result.Error!);
        }

        var list = new List<T>();
        await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            list.Add(item);
        }

        return Result<T[], TError>.Success(list.ToArray());
    }

    /// <summary>
    ///     Batches items in an AsyncEnumerableResult.
    /// </summary>
    public static AsyncEnumerableResult<IReadOnlyList<T>, TError> Batch<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        int batchSize)
        where TError : ResultError
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<IReadOnlyList<T>, TError>.Failure(result.Error!);
        }

        async IAsyncEnumerable<IReadOnlyList<T>> BatchAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var batch = new List<T>(batchSize);

            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                batch.Add(item);

                if (batch.Count == batchSize)
                {
                    yield return batch.ToArray();
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                yield return batch.ToArray();
            }
        }

        return AsyncEnumerableResult<IReadOnlyList<T>, TError>.Success(BatchAsync());
    }

    /// <summary>
    ///     Takes a specified number of items from an AsyncEnumerableResult.
    /// </summary>
    public static AsyncEnumerableResult<T, TError> Take<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        int count)
        where TError : ResultError
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (!result.IsSuccess)
        {
            return result;
        }

        if (count == 0)
        {
            return AsyncEnumerableResult<T, TError>.Success(EmptyAsync<T>());
        }

        async IAsyncEnumerable<T> TakeAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var taken = 0;

            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (taken >= count)
                {
                    yield break;
                }

                yield return item;
                taken++;
            }
        }

        return AsyncEnumerableResult<T, TError>.Success(TakeAsync());
    }

    /// <summary>
    ///     Skips a specified number of items in an AsyncEnumerableResult.
    /// </summary>
    public static AsyncEnumerableResult<T, TError> Skip<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        int count)
        where TError : ResultError
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (!result.IsSuccess)
        {
            return result;
        }

        if (count == 0)
        {
            return result;
        }

        async IAsyncEnumerable<T> SkipAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var skipped = 0;

            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (skipped < count)
                {
                    skipped++;
                    continue;
                }

                yield return item;
            }
        }

        return AsyncEnumerableResult<T, TError>.Success(SkipAsync());
    }

    /// <summary>
    ///     Aggregates items in an AsyncEnumerableResult using an accumulator function.
    /// </summary>
    public static async ValueTask<Result<TAccumulate, TError>> AggregateAsync<T, TAccumulate, TError>(
        this AsyncEnumerableResult<T, TError> result,
        TAccumulate seed,
        Func<TAccumulate, T, TAccumulate> accumulator,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(accumulator);

        if (!result.IsSuccess)
        {
            return Result<TAccumulate, TError>.Failure(result.Error!);
        }

        var current = seed;

        await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            current = accumulator(current, item);
        }

        return Result<TAccumulate, TError>.Success(current);
    }

    /// <summary>
    ///     Counts the items in an AsyncEnumerableResult.
    /// </summary>
    public static async ValueTask<Result<int, TError>> CountAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<int, TError>.Failure(result.Error!);
        }

        var count = 0;

        await foreach (var _ in result.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            count++;
        }

        return Result<int, TError>.Success(count);
    }

    /// <summary>
    ///     Checks if any items in the AsyncEnumerableResult match a predicate.
    /// </summary>
    public static async ValueTask<Result<bool, TError>> AnyAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, bool>? predicate = null,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<bool, TError>.Failure(result.Error!);
        }

        await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (predicate == null || predicate(item))
            {
                return Result<bool, TError>.Success(true);
            }
        }

        return Result<bool, TError>.Success(false);
    }

    /// <summary>
    ///     Checks if all items in the AsyncEnumerableResult match a predicate.
    /// </summary>
    public static async ValueTask<Result<bool, TError>> AllAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, bool> predicate,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (!result.IsSuccess)
        {
            return Result<bool, TError>.Failure(result.Error!);
        }

        await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!predicate(item))
            {
                return Result<bool, TError>.Success(false);
            }
        }

        return Result<bool, TError>.Success(true);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates an empty async enumerable.
    /// </summary>
    private static async IAsyncEnumerable<T> EmptyAsync<T>(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    #endregion
}
