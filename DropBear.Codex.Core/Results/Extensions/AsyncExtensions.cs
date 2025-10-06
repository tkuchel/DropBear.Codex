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

    #region Parallel Async Processing

    /// <summary>
    ///     Processes items from an async enumerable in parallel with controlled concurrency.
    ///     Uses modern .NET 9 patterns for optimal performance.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<TResult>, TError>> ParallelSelectAsync<T, TResult, TError>(
        this IAsyncEnumerable<T> source,
        Func<T, CancellationToken, ValueTask<TResult>> selector,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        try
        {
            // Materialize to array first
            var items = new List<T>();
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                items.Add(item);
            }

            if (items.Count == 0)
            {
                return Result<IReadOnlyList<TResult>, TError>.Success(Array.Empty<TResult>());
            }

            var results = new TResult[items.Count];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism, CancellationToken = cancellationToken
            };

            await Parallel.ForAsync(0, items.Count, options, async (i, ct) =>
            {
                results[i] = await selector(items[i], ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            return Result<IReadOnlyList<TResult>, TError>.Success(results);
        }
        catch (OperationCanceledException)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), "Operation was cancelled")!;
            return Result<IReadOnlyList<TResult>, TError>.Cancelled(errorInstance);
        }
        catch (Exception ex)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), $"Parallel operation failed: {ex.Message}")!;
            return Result<IReadOnlyList<TResult>, TError>.Failure(errorInstance, ex);
        }
    }

    #endregion

    #region Buffering and Batching

    /// <summary>
    ///     Buffers items from an async enumerable into batches.
    ///     Uses modern Chunk() method for efficient batching.
    /// </summary>
    public static AsyncEnumerableResult<IReadOnlyList<T>, TError> Buffer<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        int bufferSize)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);

        if (bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be positive");
        }

        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<IReadOnlyList<T>, TError>.Failure(result.Error!);
        }

        async IAsyncEnumerable<IReadOnlyList<T>> BufferAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var buffer = new List<T>(bufferSize);

            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                buffer.Add(item);

                if (buffer.Count >= bufferSize)
                {
                    yield return buffer.AsReadOnly();
                    buffer = new List<T>(bufferSize);
                }
            }

            // Yield remaining items if any
            if (buffer.Count > 0)
            {
                yield return buffer.AsReadOnly();
            }
        }

        return AsyncEnumerableResult<IReadOnlyList<T>, TError>.Success(BufferAsync());
    }

    #endregion

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
        ArgumentNullException.ThrowIfNull(result);
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
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation
        }
        catch (Exception ex)
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
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(binderAsync);

        if (!result.IsSuccess)
        {
            return Result<TResult, TError>.Failure(result.Error!);
        }

        try
        {
            return await binderAsync(result.Value!, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation
        }
        catch (Exception ex)
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

    #region AsyncEnumerableResult Extensions

    /// <summary>
    ///     Filters items in an AsyncEnumerableResult based on a predicate.
    /// </summary>
    public static AsyncEnumerableResult<T, TError> Where<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, bool> predicate)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
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
        ArgumentNullException.ThrowIfNull(result);
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
        ArgumentNullException.ThrowIfNull(result);
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
        ArgumentNullException.ThrowIfNull(result);
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
        ArgumentNullException.ThrowIfNull(result);
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
                await foreach (var nestedItem in selector(item).WithCancellation(cancellationToken)
                                   .ConfigureAwait(false))
                {
                    yield return nestedItem;
                }
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(SelectManyAsync());
    }

    /// <summary>
    ///     Takes the first N items from an AsyncEnumerableResult.
    /// </summary>
    public static AsyncEnumerableResult<T, TError> Take<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        int count)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative");
        }

        if (!result.IsSuccess)
        {
            return result;
        }

        async IAsyncEnumerable<T> TakeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
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
    ///     Skips the first N items from an AsyncEnumerableResult.
    /// </summary>
    public static AsyncEnumerableResult<T, TError> Skip<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        int count)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative");
        }

        if (!result.IsSuccess)
        {
            return result;
        }

        async IAsyncEnumerable<T> SkipAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
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
    ///     Materializes an AsyncEnumerableResult to a list.
    ///     Uses modern collection expressions for the result.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<T>, TError>> ToListAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            return Result<IReadOnlyList<T>, TError>.Failure(result.Error!);
        }

        try
        {
            var list = new List<T>();

            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                list.Add(item);
            }

            return Result<IReadOnlyList<T>, TError>.Success(list.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), "Operation was cancelled")!;
            return Result<IReadOnlyList<T>, TError>.Cancelled(errorInstance);
        }
        catch (Exception ex)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), $"Failed to materialize async enumerable: {ex.Message}")!;
            return Result<IReadOnlyList<T>, TError>.Failure(errorInstance, ex);
        }
    }

    /// <summary>
    ///     Materializes an AsyncEnumerableResult to an array.
    ///     More memory efficient than ToListAsync for known-size collections.
    /// </summary>
    public static async ValueTask<Result<T[], TError>> ToArrayAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            return Result<T[], TError>.Failure(result.Error!);
        }

        try
        {
            var list = new List<T>();

            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                list.Add(item);
            }

            return Result<T[], TError>.Success(list.ToArray());
        }
        catch (OperationCanceledException)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), "Operation was cancelled")!;
            return Result<T[], TError>.Cancelled(errorInstance);
        }
        catch (Exception ex)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), $"Failed to materialize async enumerable: {ex.Message}")!;
            return Result<T[], TError>.Failure(errorInstance, ex);
        }
    }

    /// <summary>
    ///     Executes an action for each item in an AsyncEnumerableResult.
    /// </summary>
    public static async ValueTask<Result<Unit, TError>> ForEachAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, ValueTask> action,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);

        if (!result.IsSuccess)
        {
            return Result<Unit, TError>.Failure(result.Error!);
        }

        try
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await action(item).ConfigureAwait(false);
            }

            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), "Operation was cancelled")!;
            return Result<Unit, TError>.Cancelled(errorInstance);
        }
        catch (Exception ex)
        {
            var errorInstance = (TError)Activator.CreateInstance(
                typeof(TError), $"ForEach operation failed: {ex.Message}")!;
            return Result<Unit, TError>.Failure(errorInstance, ex);
        }
    }

    #endregion
}
