#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

#endregion

namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Provides extension methods for working with Result types.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    ///     Combines multiple results into a single result.
    /// </summary>
    public static Result<IReadOnlyList<T>, TError> Combine<T, TError>(
        this IEnumerable<Result<T, TError>> results,
        Func<IEnumerable<TError>, TError> errorAggregator)
        where TError : ResultError
    {
        var successValues = new List<T>();
        var errors = new List<TError>();

        foreach (var result in results)
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
            return Result<IReadOnlyList<T>, TError>.Success(successValues);
        }

        return errors.Count == results.Count()
            ? Result<IReadOnlyList<T>, TError>.Failure(errorAggregator(errors))
            : Result<IReadOnlyList<T>, TError>.PartialSuccess(successValues, errorAggregator(errors));
    }

    /// <summary>
    ///     Combines multiple results asynchronously into a single result.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<T>, TError>> CombineAsync<T, TError>(
        this IEnumerable<Task<Result<T, TError>>> results,
        Func<IEnumerable<TError>, TError> errorAggregator)
        where TError : ResultError
    {
        var completedResults = await Task.WhenAll(results).ConfigureAwait(false);
        return completedResults.Combine(errorAggregator);
    }

    /// <summary>
    ///     Executes an async operation for each item in parallel, with a maximum degree of parallelism.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<TResult>, TError>> ParallelForEachAsync<T, TResult, TError>(
        this IEnumerable<T> source,
        Func<T, CancellationToken, ValueTask<Result<TResult, TError>>> operation,
        int maxDegreeOfParallelism,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        var items = source.ToList();
        var results = new ConcurrentBag<Result<TResult, TError>>();
        var exceptions = new ConcurrentBag<Exception>();

        async ValueTask ProcessItem(T item)
        {
            try
            {
                var result = await operation(item, cancellationToken).ConfigureAwait(false);
                results.Add(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                exceptions.Add(ex);
            }
        }

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

        foreach (var item in items)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await ProcessItem(item).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (exceptions.Any())
        {
            var aggregateException = new AggregateException(exceptions);
            return Result<IReadOnlyList<TResult>, TError>.Failure(
                CreateError<TError>("Parallel operation failed with multiple exceptions", aggregateException));
        }

        return results.ToList().Combine(errors =>
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
        var batches = new List<List<T>>();
        var currentBatch = new List<T>(batchSize);

        foreach (var item in source)
        {
            currentBatch.Add(item);
            if (currentBatch.Count == batchSize)
            {
                batches.Add(currentBatch);
                currentBatch = new List<T>(batchSize);
            }
        }

        if (currentBatch.Any())
        {
            batches.Add(currentBatch);
        }

        var results = await Task.WhenAll(batches.Select(async batch =>
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
        })).ConfigureAwait(false);

        return results.SelectMany(r => r.IsSuccess ? r.Value! : Array.Empty<TResult>())
            .ToList()
            .AsReadOnly()
            .ToResult<IReadOnlyList<TResult>, TError>();
    }

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
    ///     Converts a nullable value to a Result.
    /// </summary>
    public static Result<T, TError> ToResult<T, TError>(
        this T? value,
        Func<TError> onNull)
        where T : class
        where TError : ResultError
    {
        return value != null
            ? Result<T, TError>.Success(value)
            : Result<T, TError>.Failure(onNull());
    }

    /// <summary>
    ///     Converts a ValueTask to a Result.
    /// </summary>
    public static async ValueTask<Result<T, TError>> ToResult<T, TError>(
        this ValueTask<T> task,
        Func<Exception, TError> onError)
        where TError : ResultError
    {
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
    ///     Safely executes an operation that might throw, converting the result to a Result type.
    /// </summary>
    public static Result<T, TError> Try<T, TError>(
        Func<T> operation,
        Func<Exception, TError> onError)
        where TError : ResultError
    {
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
    ///     Safely executes an async operation that might throw, converting the result to a Result type.
    /// </summary>
    public static async ValueTask<Result<T, TError>> TryAsync<T, TError>(
        Func<ValueTask<T>> operation,
        Func<Exception, TError> onError)
        where TError : ResultError
    {
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

    private static TError CreateError<TError>(string message, Exception? exception = null) where TError : ResultError
    {
        return (TError)Activator.CreateInstance(typeof(TError), message)!;
    }
}
