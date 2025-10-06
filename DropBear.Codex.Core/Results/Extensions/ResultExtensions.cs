#region

using System.Buffers;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides extension methods for Result types with modern .NET 9 optimizations.
///     Includes parallel processing, batch operations, and retry logic.
/// </summary>
public static class ResultExtensions
{
    #region Retry Logic (Modern .NET 9)

    /// <summary>
    ///     Retries an operation with exponential backoff.
    ///     Uses modern TimeProvider for testability.
    /// </summary>
    public static async ValueTask<Result<T, TError>> RetryAsync<T, TError>(
        this Func<CancellationToken, ValueTask<Result<T, TError>>> operation,
        int maxAttempts = 3,
        TimeSpan? initialDelay = null,
        double backoffMultiplier = 2.0,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be positive");
        }

        var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        var provider = timeProvider ?? TimeProvider.System;
        Result<T, TError>? lastResult = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lastResult = await operation(cancellationToken).ConfigureAwait(false);

            if (lastResult.IsSuccess)
            {
                return lastResult;
            }

            // Don't delay after the last attempt
            if (attempt < maxAttempts)
            {
                var currentDelay =
                    TimeSpan.FromMilliseconds(delay.TotalMilliseconds * Math.Pow(backoffMultiplier, attempt - 1));
                await Task.Delay(currentDelay, provider, cancellationToken).ConfigureAwait(false);
            }
        }

        return lastResult!;
    }

    #endregion

    #region Match Operations

    /// <summary>
    ///     Matches the result to one of two functions based on success/failure.
    ///     Modern pattern matching with expression-bodied members.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult Match<T, TError, TResult>(
        this Result<T, TError> result,
        Func<T, TResult> onSuccess,
        Func<TError, Exception?, TResult> onFailure)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result.IsSuccess
            ? onSuccess(result.Value!)
            : onFailure(result.Error!, result.Exception);
    }

    /// <summary>
    ///     Matches the result to one of two actions based on success/failure.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Match<T, TError>(
        this Result<T, TError> result,
        Action<T> onSuccess,
        Action<TError, Exception?> onFailure)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (result.IsSuccess)
        {
            onSuccess(result.Value!);
        }
        else
        {
            onFailure(result.Error!, result.Exception);
        }
    }

    #endregion

    #region Map/Bind Operations

    /// <summary>
    ///     Maps the value of a successful result to a new value.
    ///     Uses aggressive inlining for optimal performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult, TError> Map<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, TResult> mapper)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(mapper);

        return result.IsSuccess
            ? Result<TResult, TError>.Success(mapper(result.Value!))
            : Result<TResult, TError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Binds the value of a successful result to a new result-returning function.
    ///     Flattens nested results automatically.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult, TError> Bind<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, Result<TResult, TError>> binder)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(binder);

        return result.IsSuccess
            ? binder(result.Value!)
            : Result<TResult, TError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Executes a side effect with the result's VALUE if successful.
    ///     The action receives the unwrapped value.
    ///     Use this when you want to operate on the value itself.
    /// </summary>
    /// <example>
    ///     <code>
    /// result.Tap(value => Console.WriteLine($"Got value: {value}"));
    /// </code>
    /// </example>
    public static Result<T, TError> Tap<T, TError>(
        this Result<T, TError> result,
        Action<T> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);

        if (result.IsSuccess)
        {
            action(result.Value!);
        }

        return result;
    }

    #endregion

    #region Async Transform Extensions

    /// <summary>
    ///     Transforms a Result using an async function, optimized with ValueTask.
    /// </summary>
    public static async ValueTask<Result<TResult, TError>> MapAsync<TSource, TResult, TError>(
        this Result<TSource, TError> result,
        Func<TSource, ValueTask<TResult>> transform)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(transform);

        return result.IsSuccess
            ? Result<TResult, TError>.Success(await transform(result.Value!).ConfigureAwait(false))
            : Result<TResult, TError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Transforms a Result using an async function that can fail.
    /// </summary>
    public static async ValueTask<Result<TResult, TError>> BindAsync<TSource, TResult, TError>(
        this Result<TSource, TError> result,
        Func<TSource, ValueTask<Result<TResult, TError>>> transform)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(transform);

        return result.IsSuccess
            ? await transform(result.Value!).ConfigureAwait(false)
            : Result<TResult, TError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Executes an async action if the result is successful, without changing the result.
    /// </summary>
    public static async ValueTask<Result<TValue, TError>> TapAsync<TValue, TError>(
        this Result<TValue, TError> result,
        Func<TValue, ValueTask> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);

        if (result.IsSuccess)
        {
            await action(result.Value!).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    ///     Provides a fallback value if the result is a failure.
    /// </summary>
    public static async ValueTask<Result<TValue, TError>> OrElseAsync<TValue, TError>(
        this Result<TValue, TError> result,
        Func<TError, ValueTask<TValue>> fallbackProvider)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(fallbackProvider);

        return result.IsSuccess
            ? result
            : Result<TValue, TError>.Success(await fallbackProvider(result.Error!).ConfigureAwait(false));
    }

    #endregion

    #region Parallelism Extensions (Modern .NET 9)

    /// <summary>
    ///     Executes an async operation for each item in parallel with enhanced performance.
    ///     Uses modern Parallel.ForAsync patterns for optimal throughput.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<TResult>, TError>> ParallelMapAsync<T, TResult, TError>(
        this IEnumerable<T> source,
        Func<T, CancellationToken, ValueTask<Result<TResult, TError>>> mapper,
        int maxDegreeOfParallelism = -1,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);

        var sourceArray = source as T[] ?? source.ToArray();
        if (sourceArray.Length == 0)
        {
            return Result<IReadOnlyList<TResult>, TError>.Success(Array.Empty<TResult>());
        }

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism, CancellationToken = cancellationToken
        };

        var results = new Result<TResult, TError>[sourceArray.Length];

        await Parallel.ForAsync(0, sourceArray.Length, options, async (i, ct) =>
        {
            results[i] = await mapper(sourceArray[i], ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return results.Combine(errors =>
            (TError)Activator.CreateInstance(typeof(TError), $"{errors.Count()} operations failed")!);
    }

    /// <summary>
    ///     Executes operations in batches with configurable batch size.
    ///     Optimized for .NET 9 with collection expressions and Chunk().
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<TResult>, TError>> BatchMapAsync<T, TResult, TError>(
        this IEnumerable<T> source,
        Func<T, CancellationToken, ValueTask<Result<TResult, TError>>> mapper,
        int batchSize = 10,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive");
        }

        var allResults = new List<Result<TResult, TError>>();

        // Use modern Chunk() method for batching
        foreach (var batch in source.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchTasks = batch.Select(item => mapper(item, cancellationToken).AsTask());
            var batchResults = await Task.WhenAll(batchTasks).ConfigureAwait(false);

            allResults.AddRange(batchResults);
        }

        return allResults.Combine(errors =>
            (TError)Activator.CreateInstance(typeof(TError), $"{errors.Count()} operations failed")!);
    }

    #endregion

    #region Try/Catch Extensions

    /// <summary>
    ///     Safely executes an operation that might throw, converting the result to a Result type.
    ///     Optimized for .NET 9 with better exception handling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Try<T, TError>(
        Func<T> operation,
        Func<Exception, TError> onError)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(onError);

        try
        {
            return Result<T, TError>.Success(operation());
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(onError(ex), ex);
        }
    }

    /// <summary>
    ///     Safely executes an async operation that might throw.
    /// </summary>
    public static async ValueTask<Result<T, TError>> TryAsync<T, TError>(
        Func<ValueTask<T>> operation,
        Func<Exception, TError> onError)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(onError);

        try
        {
            var result = await operation().ConfigureAwait(false);
            return Result<T, TError>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(onError(ex), ex);
        }
    }

    #endregion

    #region Collection Result Extensions (Modern .NET 9)

    /// <summary>
    ///     Combines multiple results into a single result.
    ///     Optimized with Span and modern collection expressions (.NET 9).
    /// </summary>
    public static Result<IReadOnlyList<T>, TError> Combine<T, TError>(
        this IEnumerable<Result<T, TError>> results,
        Func<IEnumerable<TError>, TError> errorAggregator)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(errorAggregator);

        // Convert to array for efficient iteration
        var resultArray = results as Result<T, TError>[] ?? results.ToArray();

        if (resultArray.Length == 0)
        {
            return Result<IReadOnlyList<T>, TError>.Success(Array.Empty<T>());
        }

        // Use ArrayPool for temporary storage to reduce allocations
        var successValues = new List<T>(resultArray.Length);
        var errors = new List<TError>();

        // Single pass through results
        foreach (var result in resultArray)
        {
            if (result.IsSuccess)
            {
                successValues.Add(result.Value!);
            }
            else if (result.Error is not null)
            {
                errors.Add(result.Error);
            }
        }

        // Return based on error state
        return errors.Count switch
        {
            0 => Result<IReadOnlyList<T>, TError>.Success(successValues.AsReadOnly()),
            _ when errors.Count == resultArray.Length => Result<IReadOnlyList<T>, TError>.Failure(
                errorAggregator(errors)),
            _ => Result<IReadOnlyList<T>, TError>.PartialSuccess(successValues.AsReadOnly(), errorAggregator(errors))
        };
    }

    /// <summary>
    ///     Combines multiple async results into a single result with enhanced parallelism.
    ///     Uses modern async patterns for optimal performance.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<T>, TError>> CombineAsync<T, TError>(
        this IEnumerable<ValueTask<Result<T, TError>>> results,
        Func<IEnumerable<TError>, TError> errorAggregator,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(errorAggregator);

        var resultArray = results.ToArray();
        if (resultArray.Length == 0)
        {
            return Result<IReadOnlyList<T>, TError>.Success(Array.Empty<T>());
        }

        var completedResults = new Result<T, TError>[resultArray.Length];

        // Process all tasks
        for (var i = 0; i < resultArray.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            completedResults[i] = await resultArray[i].ConfigureAwait(false);
        }

        return completedResults.Combine(errorAggregator);
    }

    /// <summary>
    ///     Combines Task-based results with efficient awaiting.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<T>, TError>> CombineAsync<T, TError>(
        this IEnumerable<Task<Result<T, TError>>> results,
        Func<IEnumerable<TError>, TError> errorAggregator,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(errorAggregator);

        var completedResults = await Task.WhenAll(results).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return completedResults.Combine(errorAggregator);
    }

    #endregion

    #region Performance-Optimized Hot Paths

    /// <summary>
    ///     Combines results using ArrayPool to reduce allocations.
    ///     Performance-optimized version for hot paths.
    /// </summary>
    public static Result<IReadOnlyList<T>, TError> CombineOptimized<T, TError>(
        ReadOnlySpan<Result<T, TError>> results,
        Func<ReadOnlySpan<TError>, TError> errorAggregator)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(errorAggregator);

        if (results.IsEmpty)
        {
            return Result<IReadOnlyList<T>, TError>.Success([]);
        }

        // Rent arrays from pool to avoid heap allocation
        var successValuesArray = ArrayPool<T>.Shared.Rent(results.Length);
        var errorsArray = ArrayPool<TError>.Shared.Rent(results.Length);

        try
        {
            var successCount = 0;
            var errorCount = 0;

            // Single pass through results
            for (var i = 0; i < results.Length; i++)
            {
                var result = results[i];

                if (result.IsSuccess)
                {
                    successValuesArray[successCount++] = result.Value!;
                }
                else if (result.Error is not null)
                {
                    errorsArray[errorCount++] = result.Error;
                }
            }

            // Return based on error state using sliced spans
            if (errorCount == 0)
            {
                var successList = successValuesArray.AsSpan(0, successCount).ToArray();
                return Result<IReadOnlyList<T>, TError>.Success(successList);
            }

            if (errorCount == results.Length)
            {
                var errorList = errorsArray.AsSpan(0, errorCount);
                return Result<IReadOnlyList<T>, TError>.Failure(errorAggregator(errorList));
            }

            // Partial success
            var partialSuccessList = successValuesArray.AsSpan(0, successCount).ToArray();
            var partialErrorList = errorsArray.AsSpan(0, errorCount);
            return Result<IReadOnlyList<T>, TError>.PartialSuccess(
                partialSuccessList,
                errorAggregator(partialErrorList));
        }
        finally
        {
            // Always return arrays to pool
            ArrayPool<T>.Shared.Return(successValuesArray, true);
            ArrayPool<TError>.Shared.Return(errorsArray, true);
        }
    }

    /// <summary>
    ///     Fast path for mapping when the mapper is known to not throw.
    ///     Uses aggressive inlining for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult, TError> MapUnchecked<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, TResult> mapper)
        where TError : ResultError
    {
        // No null checks - caller guarantees non-null
        // No exception handling - caller guarantees no throws
        return result.IsSuccess
            ? Result<TResult, TError>.Success(mapper(result.Value!))
            : Result<TResult, TError>.Failure(result.Error!);
    }


    /// <summary>
    ///     Batches items using ArrayPool for memory efficiency.
    ///     Uses collection-based processing for optimal performance.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<TResult>, TError>> BatchMapOptimizedAsync<T, TResult, TError>(
        this IEnumerable<T> source,
        Func<T, CancellationToken, ValueTask<Result<TResult, TError>>> mapper,
        int batchSize = 10,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive");
        }

        var sourceList = source as IReadOnlyList<T> ?? source.ToList();

        if (sourceList.Count == 0)
        {
            return Result<IReadOnlyList<TResult>, TError>.Success([]);
        }

        var allResults = new List<Result<TResult, TError>>(sourceList.Count);

        // Process in batches
        for (var offset = 0; offset < sourceList.Count; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchLength = Math.Min(batchSize, sourceList.Count - offset);

            // Rent array from pool for batch tasks
            var batchTasks = ArrayPool<ValueTask<Result<TResult, TError>>>.Shared.Rent(batchLength);

            try
            {
                // Create tasks for batch
                for (var i = 0; i < batchLength; i++)
                {
                    var item = sourceList[offset + i];
                    batchTasks[i] = mapper(item, cancellationToken);
                }

                // Await all tasks in batch
                for (var i = 0; i < batchLength; i++)
                {
                    allResults.Add(await batchTasks[i].ConfigureAwait(false));
                }
            }
            finally
            {
                // Return array to pool
                ArrayPool<ValueTask<Result<TResult, TError>>>.Shared.Return(batchTasks, true);
            }
        }

        return allResults.Combine(errors =>
            (TError)Activator.CreateInstance(typeof(TError), $"{errors.Count()} operations failed")!);
    }

    #endregion

    #region Conversion Extensions

    /// <summary>
    ///     Converts a Task to a Result, handling exceptions.
    ///     Modern async pattern with ValueTask return.
    /// </summary>
    public static async ValueTask<Result<T, TError>> ToResultAsync<T, TError>(
        this Task<T> task,
        Func<Exception, TError> onError)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onError);

        try
        {
            var result = await task.ConfigureAwait(false);
            return Result<T, TError>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(onError(ex), ex);
        }
    }

    /// <summary>
    ///     Deconstructs a result into its components for pattern matching.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deconstruct<T, TError>(
        this Result<T, TError> result,
        out bool success,
        out T? value,
        out TError? error)
        where TError : ResultError
    {
        success = result.IsSuccess;
        value = result.Value;
        error = result.Error;
    }

    #endregion
}
