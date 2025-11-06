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
    /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="enumerable">The async enumerable to convert.</param>
    /// <returns>A successful AsyncEnumerableResult wrapping the enumerable.</returns>
    /// <exception cref="ArgumentNullException">Thrown when enumerable is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AsyncEnumerableResult<T, TError> ToResult<T, TError>(
        this IAsyncEnumerable<T> enumerable)
        where TError : ResultError, new()
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
    /// <typeparam name="T">The source element type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="source">The source async enumerable.</param>
    /// <param name="selector">The transformation function.</param>
    /// <param name="maxDegreeOfParallelism">Maximum degree of parallelism (-1 for unlimited).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the transformed items or an error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when source or selector is null.</exception>
    /// <remarks>
    ///     Warning: This method materializes the entire enumerable into memory before processing.
    ///     For large datasets, consider using batch processing instead.
    /// </remarks>
    public static async ValueTask<Result<IReadOnlyList<TResult>, TError>> ParallelSelectAsync<T, TResult, TError>(
        this IAsyncEnumerable<T> source,
        Func<T, CancellationToken, ValueTask<TResult>> selector,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        try
        {
            // Materialize to array first - Note: This loads entire enumerable into memory
            var items = new List<T>();
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                items.Add(item);
            }

            if (items.Count == 0)
            {
                return Result<IReadOnlyList<TResult>, TError>.Success([]);
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
            return Result<IReadOnlyList<TResult>, TError>.Cancelled(new TError
            {
                Message = "Parallel operation was cancelled"
            });
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<TResult>, TError>.Failure(
                new TError { Message = "Parallel operation failed" },
                ex);
        }
    }

    #endregion

    #region Buffering and Batching

    /// <summary>
    ///     Buffers items from an async enumerable into batches using the Chunk() method.
    ///     Optimized for .NET 9 with efficient batching.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result to buffer.</param>
    /// <param name="bufferSize">The size of each buffer batch.</param>
    /// <returns>An AsyncEnumerableResult of buffered items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when bufferSize is not positive.</exception>
    public static AsyncEnumerableResult<IReadOnlyList<T>, TError> Buffer<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        int bufferSize)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<IReadOnlyList<T>, TError>.Failure(result.Error!);
        }

        async IAsyncEnumerable<IReadOnlyList<T>> BufferInternal(
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

        return AsyncEnumerableResult<IReadOnlyList<T>, TError>.Success(BufferInternal());
    }

    #endregion

    #region Async Result Extensions

    /// <summary>
    ///     Asynchronously maps a Result to a new Result with a transformed value.
    /// </summary>
    /// <typeparam name="T">The source value type.</typeparam>
    /// <typeparam name="TResult">The result value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="mapperAsync">The async transformation function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result with the transformed value or the original error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result or mapperAsync is null.</exception>
    public static async ValueTask<Result<TResult, TError>> MapAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, CancellationToken, ValueTask<TResult>> mapperAsync,
        CancellationToken cancellationToken = default)
        where TError : ResultError, new()
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
    /// <typeparam name="T">The source value type.</typeparam>
    /// <typeparam name="TResult">The result value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="binderAsync">The async binding function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bound Result or the original error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result or binderAsync is null.</exception>
    public static async ValueTask<Result<TResult, TError>> BindAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, CancellationToken, ValueTask<Result<TResult, TError>>> binderAsync,
        CancellationToken cancellationToken = default)
        where TError : ResultError, new()
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
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="resultTask">The task to flatten.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The flattened Result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when resultTask is null.</exception>
    public static async ValueTask<Result<T, TError>> FlattenAsync<T, TError>(
        this Task<Result<T, TError>> resultTask,
        CancellationToken cancellationToken = default)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(resultTask);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result<T, TError>.Cancelled(new TError { Message = "Operation was cancelled" });
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(new TError { Message = "Task failed" }, ex);
        }
    }

    /// <summary>
    ///     Converts a ValueTask returning a Result to a flat Result.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="resultTask">The value task to flatten.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The flattened Result.</returns>
    public static async ValueTask<Result<T, TError>> FlattenAsync<T, TError>(
        this ValueTask<Result<T, TError>> resultTask,
        CancellationToken cancellationToken = default)
        where TError : ResultError, new()
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result<T, TError>.Cancelled(new TError { Message = "Operation was cancelled" });
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(new TError { Message = "ValueTask failed" }, ex);
        }
    }

    #endregion

    #region AsyncEnumerableResult Extensions

    /// <summary>
    ///     Filters items in an AsyncEnumerableResult based on a predicate.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result to filter.</param>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>A filtered AsyncEnumerableResult.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result or predicate is null.</exception>
    public static AsyncEnumerableResult<T, TError> Where<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, bool> predicate)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(predicate);

        if (!result.IsSuccess)
        {
            return result;
        }

        async IAsyncEnumerable<T> FilterInternal(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (predicate(item))
                {
                    yield return item;
                }
            }
        }

        return AsyncEnumerableResult<T, TError>.Success(FilterInternal());
    }

    /// <summary>
    ///     Filters items asynchronously in an AsyncEnumerableResult.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result to filter.</param>
    /// <param name="predicateAsync">The async filter predicate.</param>
    /// <returns>A filtered AsyncEnumerableResult.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result or predicateAsync is null.</exception>
    public static AsyncEnumerableResult<T, TError> WhereAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, ValueTask<bool>> predicateAsync)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(predicateAsync);

        if (!result.IsSuccess)
        {
            return result;
        }

        async IAsyncEnumerable<T> FilterInternal(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await predicateAsync(item).ConfigureAwait(false))
                {
                    yield return item;
                }
            }
        }

        return AsyncEnumerableResult<T, TError>.Success(FilterInternal());
    }

    /// <summary>
    ///     Maps items in an AsyncEnumerableResult using a transformation function.
    /// </summary>
    /// <typeparam name="T">The source element type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result to transform.</param>
    /// <param name="selector">The transformation function.</param>
    /// <returns>A transformed AsyncEnumerableResult.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result or selector is null.</exception>
    public static AsyncEnumerableResult<TResult, TError> Select<T, TResult, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, TResult> selector)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(selector);

        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(result.Error!);
        }

        // FIXED: Renamed from SelectAsync to SelectInternal to avoid collision
        async IAsyncEnumerable<TResult> SelectInternal(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return selector(item);
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(SelectInternal());
    }

    /// <summary>
    ///     Applies an async transformation to each item in an AsyncEnumerableResult.
    /// </summary>
    /// <typeparam name="T">The source element type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result to transform.</param>
    /// <param name="selector">The async transformation function.</param>
    /// <returns>A transformed AsyncEnumerableResult.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result or selector is null.</exception>
    public static AsyncEnumerableResult<TResult, TError> SelectAsync<T, TResult, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, ValueTask<TResult>> selector)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(selector);

        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(result.Error!);
        }

        async IAsyncEnumerable<TResult> SelectAsyncInternal(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return await selector(item).ConfigureAwait(false);
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(SelectAsyncInternal());
    }

    /// <summary>
    ///     Flattens nested async enumerables using SelectMany.
    /// </summary>
    /// <typeparam name="T">The source element type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result to flatten.</param>
    /// <param name="selector">The selector function returning nested enumerables.</param>
    /// <returns>A flattened AsyncEnumerableResult.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result or selector is null.</exception>
    public static AsyncEnumerableResult<TResult, TError> SelectMany<T, TResult, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, IAsyncEnumerable<TResult>> selector)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(selector);

        if (!result.IsSuccess)
        {
            return AsyncEnumerableResult<TResult, TError>.Failure(result.Error!);
        }

        async IAsyncEnumerable<TResult> SelectManyInternal(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in result.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await foreach (var nestedItem in selector(item)
                                   .WithCancellation(cancellationToken)
                                   .ConfigureAwait(false))
                {
                    yield return nestedItem;
                }
            }
        }

        return AsyncEnumerableResult<TResult, TError>.Success(SelectManyInternal());
    }

    /// <summary>
    ///     Takes the first N items from an AsyncEnumerableResult.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result.</param>
    /// <param name="count">The number of items to take.</param>
    /// <returns>An AsyncEnumerableResult with the first N items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when count is negative.</exception>
    public static AsyncEnumerableResult<T, TError> Take<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        int count)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (!result.IsSuccess)
        {
            return result;
        }

        async IAsyncEnumerable<T> TakeInternal(
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

        return AsyncEnumerableResult<T, TError>.Success(TakeInternal());
    }

    /// <summary>
    ///     Skips the first N items from an AsyncEnumerableResult.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result.</param>
    /// <param name="count">The number of items to skip.</param>
    /// <returns>An AsyncEnumerableResult with items after the first N.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when count is negative.</exception>
    public static AsyncEnumerableResult<T, TError> Skip<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        int count)
        where TError : ResultError, new()
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (!result.IsSuccess)
        {
            return result;
        }

        async IAsyncEnumerable<T> SkipInternal(
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

        return AsyncEnumerableResult<T, TError>.Success(SkipInternal());
    }

    /// <summary>
    ///     Materializes an AsyncEnumerableResult to a list.
    ///     Uses modern collection expressions for the result.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result to materialize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing a list of all items or an error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result is null.</exception>
    public static async ValueTask<Result<IReadOnlyList<T>, TError>> ToListAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        CancellationToken cancellationToken = default)
        where TError : ResultError, new()
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
                cancellationToken.ThrowIfCancellationRequested();
                list.Add(item);
            }

            return Result<IReadOnlyList<T>, TError>.Success(list.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
            return Result<IReadOnlyList<T>, TError>.Cancelled(
                new TError { Message = "Operation was cancelled" });
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<T>, TError>.Failure(
                new TError { Message = "Failed to materialize async enumerable" },
                ex);
        }
    }

    /// <summary>
    ///     Materializes an AsyncEnumerableResult to an array.
    ///     More memory efficient than ToListAsync for known-size collections.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result to materialize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing an array of all items or an error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result is null.</exception>
    public static async ValueTask<Result<T[], TError>> ToArrayAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        CancellationToken cancellationToken = default)
        where TError : ResultError, new()
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
                cancellationToken.ThrowIfCancellationRequested();
                list.Add(item);
            }

            return Result<T[], TError>.Success(list.ToArray());
        }
        catch (OperationCanceledException)
        {
            return Result<T[], TError>.Cancelled(
                new TError { Message = "Operation was cancelled" });
        }
        catch (Exception ex)
        {
            return Result<T[], TError>.Failure(
                new TError { Message = "Failed to materialize async enumerable" },
                ex);
        }
    }

    /// <summary>
    ///     Executes an action for each item in an AsyncEnumerableResult.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The async enumerable result.</param>
    /// <param name="action">The action to execute for each item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result indicating success or failure of the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when result or action is null.</exception>
    public static async ValueTask<Result<Unit, TError>> ForEachAsync<T, TError>(
        this AsyncEnumerableResult<T, TError> result,
        Func<T, ValueTask> action,
        CancellationToken cancellationToken = default)
        where TError : ResultError, new()
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
                cancellationToken.ThrowIfCancellationRequested();
                await action(item).ConfigureAwait(false);
            }

            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            return Result<Unit, TError>.Cancelled(
                new TError { Message = "Operation was cancelled" });
        }
        catch (Exception ex)
        {
            return Result<Unit, TError>.Failure(
                new TError { Message = "ForEach operation failed" },
                ex);
        }
    }

    #endregion
}
