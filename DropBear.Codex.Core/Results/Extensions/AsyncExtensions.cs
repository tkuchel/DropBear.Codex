#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Async;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides extension methods for working with asynchronous Result types and enumerables.
///     Consolidates functionality from AsyncEnumerableExtensions and AsyncEnumerableResultExtensions.
/// </summary>
public static class AsyncExtensions
{
    #region Helper Methods

    /// <summary>
    ///     Creates an empty async enumerable.
    /// </summary>
    private static async IAsyncEnumerable<T> Empty<T>(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }

        await Task.CompletedTask.ConfigureAwait(false); // Make this method truly async
    }

    #endregion

    #region Async Result Task Extensions

    /// <summary>
    ///     Asynchronously maps a Result to a new Result with a transformed value.
    /// </summary>
    public static async ValueTask<Result<TResult, TError>> MapAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, CancellationToken, ValueTask<TResult>> mapperAsync,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
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
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Create a cancelled error result
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), "Operation was cancelled")!;
            return Result<T, TError>.Cancelled(errorInstance);
        }
        catch (Exception ex)
        {
            // Create a failure result with the exception details
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), $"Task failed: {ex.Message}")!;
            return Result<T, TError>.Failure(errorInstance, ex);
        }
    }

    #endregion

    #region IAsyncEnumerable Extensions

    /// <summary>
    ///     Configures how awaiting is performed within an async enumerable.
    /// </summary>
    public static IAsyncEnumerable<T> ConfigureAwait<T>(
        this IAsyncEnumerable<T> enumerable,
        bool continueOnCapturedContext)
    {
        return new ConfiguredAsyncEnumerable<T>(enumerable, continueOnCapturedContext);
    }

    /// <summary>
    ///     Converts an IAsyncEnumerable to an AsyncEnumerableResult.
    /// </summary>
    public static AsyncEnumerableResult<T, TError> ToResult<T, TError>(
        this IAsyncEnumerable<T> enumerable)
        where TError : ResultError
    {
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
    ///     Maps items in an AsyncEnumerableResult using a transformation function.
    /// </summary>
    public static AsyncEnumerableResult<TResult, TError> Select<T, TResult, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, TResult> selector)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(result.Error!);
        }

        async IAsyncEnumerable<TResult> InternalSelectAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return selector(item);
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(InternalSelectAsync());
    }

    /// <summary>
    ///     Applies an async transformation to each item in an AsyncEnumerableResult.
    /// </summary>
    public static AsyncEnumerableResult<TResult, TError> SelectAsync<T, TResult, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, ValueTask<TResult>> selector)
        where TError : ResultError
    {
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
    ///     Batches items in an AsyncEnumerableResult.
    /// </summary>
    public static AsyncEnumerableResult<IReadOnlyList<T>, TError> Batch<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        int batchSize)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<IReadOnlyList<T>, TError>.Failure(result.Error!);
        }

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");
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
        if (!result.IsSuccess)
        {
            return result;
        }

        if (count <= 0)
        {
            return AsyncEnumerableResult<T, TError>.Success(Empty<T>());
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
        if (!result.IsSuccess)
        {
            return result;
        }

        if (count <= 0)
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

    #endregion
}
