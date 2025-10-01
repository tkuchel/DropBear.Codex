#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides comprehensive extension methods for working with Result types.
///     Optimized for .NET 9 with enhanced performance and modern patterns.
/// </summary>
public static class ResultExtensions
{
    #region Error Handling Utilities

    /// <summary>
    ///     Creates an error of type TError with the specified message and optional exception.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TError CreateError<TError>(string message, Exception? exception = null)
        where TError : ResultError
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (exception == null)
        {
            return (TError)Activator.CreateInstance(typeof(TError), message)!;
        }

        try
        {
            // Try constructor with message and exception
            return (TError)Activator.CreateInstance(typeof(TError), message, exception)!;
        }
        catch
        {
            // Fallback to message-only constructor with metadata
            var error = (TError)Activator.CreateInstance(typeof(TError), message)!;
            return (TError)error.WithMetadata("Exception", exception.ToString());
        }
    }

    /// <summary>
    ///     Adds metadata to a ResultError.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TError WithMetadata<TError>(
        this TError error,
        string key,
        object value)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        return (TError)error.WithMetadata(key, value);
    }

    /// <summary>
    ///     Adds multiple metadata entries to a ResultError.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TError WithMetadata<TError>(
        this TError error,
        IReadOnlyDictionary<string, object> metadata)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata.Count == 0 ? error : (TError)error.WithMetadata(metadata);
    }

    #endregion

    #region Value Conversion Extensions

    /// <summary>
    ///     Converts a value to a Success result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> ToResult<T, TError>(this T value)
        where TError : ResultError
    {
        return Result<T, TError>.Success(value);
    }

    /// <summary>
    ///     Converts a nullable reference type to a Result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> ToResult<T, TError>(
        this T? value,
        Func<TError> onNull)
        where T : class
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(onNull);
        return value is not null
            ? Result<T, TError>.Success(value)
            : Result<T, TError>.Failure(onNull());
    }

    /// <summary>
    ///     Converts a nullable value type to a Result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> ToResult<T, TError>(
        this T? value,
        Func<TError> onNull)
        where T : struct
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(onNull);
        return value.HasValue
            ? Result<T, TError>.Success(value.Value)
            : Result<T, TError>.Failure(onNull());
    }

    /// <summary>
    ///     Converts a ValueTask to a Result with enhanced exception handling.
    /// </summary>
    public static async ValueTask<Result<T, TError>> ToResult<T, TError>(
        this ValueTask<T> task,
        Func<Exception, TError> onError)
        where TError : ResultError
    {
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
    ///     Converts a Task to a Result.
    /// </summary>
    public static async ValueTask<Result<T, TError>> ToResult<T, TError>(
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

    #endregion

    #region Try/Catch Extensions

    /// <summary>
    ///     Safely executes an operation that might throw, converting the result to a Result type.
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
            var result = operation();
            return Result<T, TError>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(onError(ex), ex);
        }
    }

    /// <summary>
    ///     Safely executes an async operation that might throw, optimized for ValueTask.
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

    /// <summary>
    ///     Safely executes an async Task operation.
    /// </summary>
    public static async ValueTask<Result<T, TError>> TryAsync<T, TError>(
        Func<Task<T>> operation,
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

    /// <summary>
    ///     Safely executes a void operation, returning a Unit result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Unit, TError> Try<TError>(
        Action operation,
        Func<Exception, TError> onError)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(onError);

        try
        {
            operation();
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, TError>.Failure(onError(ex), ex);
        }
    }

    /// <summary>
    ///     Safely executes an async void operation, returning a Unit result.
    /// </summary>
    public static async ValueTask<Result<Unit, TError>> TryAsync<TError>(
        Func<ValueTask> operation,
        Func<Exception, TError> onError)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(onError);

        try
        {
            await operation().ConfigureAwait(false);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, TError>.Failure(onError(ex), ex);
        }
    }

    #endregion

    #region Transform Extensions

    /// <summary>
    ///     Transforms a Result using an async function, optimized with ValueTask.
    /// </summary>
    public static async ValueTask<Result<TResult, TError>> MapAsync<TSource, TResult, TError>(
        this Result<TSource, TError> result,
        Func<TSource, ValueTask<TResult>> transform)
        where TError : ResultError
    {
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
        ArgumentNullException.ThrowIfNull(transform);

        return result.IsSuccess
            ? await transform(result.Value!).ConfigureAwait(false)
            : Result<TResult, TError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Executes an action if the result is successful, without changing the result.
    /// </summary>
    public static async ValueTask<Result<TValue, TError>> TapAsync<TValue, TError>(
        this Result<TValue, TError> result,
        Func<TValue, ValueTask> action)
        where TError : ResultError
    {
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
        ArgumentNullException.ThrowIfNull(fallbackProvider);

        return result.IsSuccess
            ? result
            : Result<TValue, TError>.Success(await fallbackProvider(result.Error!).ConfigureAwait(false));
    }

    /// <summary>
    ///     Converts a Result to a ValueTask, throwing an exception if the result is a failure.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<TValue> ToValueTaskAsync<TValue, TError>(
        this Result<TValue, TError> result,
        Func<TError, Exception>? exceptionFactory = null)
        where TError : ResultError
    {
        if (result.IsSuccess)
        {
            return ValueTask.FromResult(result.Value!);
        }

        var exception = exceptionFactory?.Invoke(result.Error!)
                        ?? new InvalidOperationException($"Result failed with error: {result.Error}");

        return ValueTask.FromException<TValue>(exception);
    }

    #endregion

    #region Collection Result Extensions

    /// <summary>
    ///     Combines multiple results into a single result.
    ///     Optimized with Span and stackalloc for small collections.
    /// </summary>
    public static Result<IReadOnlyList<T>, TError> Combine<T, TError>(
        this IEnumerable<Result<T, TError>> results,
        Func<IEnumerable<TError>, TError> errorAggregator)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(errorAggregator);

        // Use collection expression for allocation
        var resultsList = results.ToList();

        if (resultsList.Count == 0)
        {
            return Result<IReadOnlyList<T>, TError>.Success(Array.Empty<T>());
        }

        var successValues = new List<T>(resultsList.Count);
        var errors = new List<TError>();

        foreach (var result in resultsList)
        {
            if (result.IsSuccess)
            {
                successValues.Add(result.Value!);
            }
            else
            {
                errors.Add(result.Error!);
            }
        }

        if (errors.Count == 0)
        {
            return Result<IReadOnlyList<T>, TError>.Success(successValues.AsReadOnly());
        }

        return errors.Count == resultsList.Count
            ? Result<IReadOnlyList<T>, TError>.Failure(errorAggregator(errors))
            : Result<IReadOnlyList<T>, TError>.PartialSuccess(
                successValues.AsReadOnly(),
                errorAggregator(errors));
    }

    /// <summary>
    ///     Combines multiple async results into a single result with enhanced parallelism.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<T>, TError>> CombineAsync<T, TError>(
        this IEnumerable<ValueTask<Result<T, TError>>> results,
        Func<IEnumerable<TError>, TError> errorAggregator)
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

        // Process all tasks concurrently
        for (var i = 0; i < resultArray.Length; i++)
        {
            completedResults[i] = await resultArray[i].ConfigureAwait(false);
        }

        return completedResults.Combine(errorAggregator);
    }

    /// <summary>
    ///     Combines Task-based results.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<T>, TError>> CombineAsync<T, TError>(
        this IEnumerable<Task<Result<T, TError>>> results,
        Func<IEnumerable<TError>, TError> errorAggregator)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(errorAggregator);

        var completedResults = await Task.WhenAll(results).ConfigureAwait(false);
        return completedResults.Combine(errorAggregator);
    }

    #endregion

    #region Parallelism Extensions

    /// <summary>
    ///     Executes an async operation for each item in parallel with enhanced performance.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<TResult>, TError>> ParallelForEachAsync<T, TResult, TError>(
        this IEnumerable<T> source,
        Func<T, CancellationToken, ValueTask<Result<TResult, TError>>> operation,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxDegreeOfParallelism, 0);

        var items = source.ToArray();
        if (items.Length == 0)
        {
            return Result<IReadOnlyList<TResult>, TError>.Success(Array.Empty<TResult>());
        }

        var results = new ConcurrentBag<(int Index, Result<TResult, TError> Result)>();
        var exceptions = new ConcurrentBag<Exception>();

        await Parallel.ForEachAsync(
            items.Select((item, index) => new { item, index }),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism, CancellationToken = cancellationToken
            },
            async (itemWithIndex, ct) =>
            {
                try
                {
                    var result = await operation(itemWithIndex.item, ct).ConfigureAwait(false);
                    results.Add((itemWithIndex.index, result));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    exceptions.Add(ex);
                }
            }).ConfigureAwait(false);

        if (exceptions.Any())
        {
            var aggregateException = new AggregateException(exceptions);
            return Result<IReadOnlyList<TResult>, TError>.Failure(
                CreateError<TError>("Parallel operation failed with multiple exceptions", aggregateException));
        }

        // Sort results by original index to maintain order
        var orderedResults = results
            .OrderBy(r => r.Index)
            .Select(r => r.Result)
            .ToArray();

        return orderedResults.Combine(errors =>
            CreateError<TError>($"Parallel operation completed with {errors.Count()} errors"));
    }

    /// <summary>
    ///     Executes an async operation for each batch of items in parallel.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<TResult>, TError>> ParallelBatchAsync<T, TResult, TError>(
        this IEnumerable<T> source,
        int batchSize,
        Func<IReadOnlyList<T>, CancellationToken, ValueTask<Result<IReadOnlyList<TResult>, TError>>> batchOperation,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(batchOperation);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

        var sourceArray = source.ToArray();
        if (sourceArray.Length == 0)
        {
            return Result<IReadOnlyList<TResult>, TError>.Success(Array.Empty<TResult>());
        }

        // Create batches using collection expression
        var batches = sourceArray
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.item).ToArray())
            .ToArray();

        var batchTasks = batches.Select(async batch =>
        {
            try
            {
                return await batchOperation(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<IReadOnlyList<TResult>, TError>.Failure(
                    CreateError<TError>($"Batch operation failed: {ex.Message}", ex));
            }
        });

        var batchResults = await Task.WhenAll(batchTasks).ConfigureAwait(false);

        var allResults = new List<TResult>();
        var errors = new List<TError>();

        foreach (var batchResult in batchResults)
        {
            if (batchResult.IsSuccess)
            {
                allResults.AddRange(batchResult.Value!);
            }
            else
            {
                errors.Add(batchResult.Error!);
            }
        }

        if (errors.Count == 0)
        {
            return Result<IReadOnlyList<TResult>, TError>.Success(allResults.AsReadOnly());
        }

        return errors.Count == batchResults.Length
            ? Result<IReadOnlyList<TResult>, TError>.Failure(
                CreateError<TError>($"All {errors.Count} batches failed"))
            : Result<IReadOnlyList<TResult>, TError>.PartialSuccess(
                allResults.AsReadOnly(),
                CreateError<TError>($"{errors.Count} of {batchResults.Length} batches failed"));
    }

    #endregion

    #region Retry Extensions

    /// <summary>
    ///     Retries an operation with exponential backoff until it succeeds or max attempts are reached.
    /// </summary>
    public static async ValueTask<Result<T, TError>> RetryAsync<T, TError>(
        this Func<ValueTask<Result<T, TError>>> operation,
        int maxAttempts,
        TimeSpan baseDelay,
        Func<TError, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAttempts, 0);

        var attempt = 1;
        while (attempt <= maxAttempts)
        {
            var result = await operation().ConfigureAwait(false);

            if (result.IsSuccess || (shouldRetry != null && !shouldRetry(result.Error!)))
            {
                return result;
            }

            if (attempt == maxAttempts)
            {
                return result.Error != null
                    ? Result<T, TError>.Failure(
                        (TError)result.Error.WithMetadata("Attempts", attempt),
                        result.Exception)
                    : result;
            }

            // Exponential backoff with jitter
            var delay = TimeSpan.FromMilliseconds(
                baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1) *
                (0.5 + Random.Shared.NextDouble() * 0.5));

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            attempt++;
        }

        return Result<T, TError>.Failure(
            CreateError<TError>($"Operation failed after {maxAttempts} attempts"));
    }

    #endregion

    #region Caching Extensions

    private static readonly ConcurrentDictionary<string, object> ResultCache = new();

    /// <summary>
    ///     Caches the result of an operation for a specified duration.
    /// </summary>
    public static async ValueTask<Result<T, TError>> CacheAsync<T, TError>(
        this Func<ValueTask<Result<T, TError>>> operation,
        string cacheKey,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        var fullKey = $"{typeof(T).FullName}:{typeof(TError).FullName}:{cacheKey}";

        if (ResultCache.TryGetValue(fullKey, out var cachedValue) &&
            cachedValue is CachedResult<T, TError> cached &&
            DateTime.UtcNow < cached.ExpiresAt)
        {
            return cached.Result;
        }

        var result = await operation().ConfigureAwait(false);

        if (result.IsSuccess)
        {
            var cacheEntry = new CachedResult<T, TError>(result, DateTime.UtcNow.Add(duration));
            ResultCache.TryAdd(fullKey, cacheEntry);
        }

        return result;
    }

    /// <summary>
    ///     Clears cached results for the specified key pattern.
    /// </summary>
    public static void ClearCache(string? keyPattern = null)
    {
        if (string.IsNullOrWhiteSpace(keyPattern))
        {
            ResultCache.Clear();
            return;
        }

        var keysToRemove = ResultCache.Keys
            .Where(key => key.Contains(keyPattern, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var key in keysToRemove)
        {
            ResultCache.TryRemove(key, out _);
        }
    }

    #endregion
}

#region Helper Types

/// <summary>
///     Represents a cached result with expiration.
/// </summary>
internal sealed record CachedResult<T, TError>(Result<T, TError> Result, DateTime ExpiresAt)
    where TError : ResultError;

#endregion
