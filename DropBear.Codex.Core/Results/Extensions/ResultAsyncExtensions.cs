#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides asynchronous extension methods for Result types
/// </summary>
public static class ResultAsyncExtensions
{
    public static async Task<Result<TResult, TError>> MapAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, Task<TResult>> mapper)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<TResult, TError>.Failure(result.Error!);
        }

        var mappedValue = await mapper(result.Value).ConfigureAwait(false);
        return Result<TResult, TError>.Success(mappedValue);
    }

    public static async Task<Result<TResult, TError>> BindAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, Task<Result<TResult, TError>>> binder)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<TResult, TError>.Failure(result.Error!);
        }

        return await binder(result.Value).ConfigureAwait(false);
    }

    public static async Task<Result<T, TError>> TimeoutAfter<T, TError>(
        this Task<Result<T, TError>> task,
        TimeSpan timeout)
        where TError : ResultError
    {
        using var cts = new CancellationTokenSource();
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token)).ConfigureAwait(false);

        if (completedTask == task)
        {
            cts.Cancel();
            return await task.ConfigureAwait(false);
        }

        try
        {
            cts.Cancel();
            await task.ConfigureAwait(false); // Allow the task to clean up
        }
        catch (Exception)
        {
            // Ignore any exceptions from cancellation
        }

        var timeoutError = new TimeoutError(timeout);
        if (timeoutError is TError typedError)
        {
            return Result<T, TError>.Failure(typedError);
        }

        throw new InvalidOperationException($"Cannot convert TimeoutError to {typeof(TError).Name}");
    }

    public static async Task<Result<T, TError>> RetryAsync<T, TError>(
        this Func<Task<Result<T, TError>>> operation,
        int maxAttempts,
        TimeSpan delay,
        Func<Exception, TError> errorMapper)
        where TError : ResultError
    {
        var exceptions = new List<Exception>();

        for (var i = 0; i < maxAttempts; i++)
        {
            try
            {
                var result = await operation().ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    return result;
                }

                if (i == maxAttempts - 1)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                if (i == maxAttempts - 1)
                {
                    var error = errorMapper(new AggregateException(exceptions));
                    return Result<T, TError>.Failure(error);
                }
            }

            if (i < maxAttempts - 1)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }
        }

        return Result<T, TError>.Failure(
            errorMapper(new InvalidOperationException($"Operation failed after {maxAttempts} attempts")));
    }

    public static async Task<Result<T, TError>> AsResultAsync<T, TError>(
        this Task<T> task,
        Func<Exception, TError> errorMapper)
        where TError : ResultError
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            return Result<T, TError>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(errorMapper(ex));
        }
    }

    public static async Task<Result<IReadOnlyList<T>, TError>> WhenAll<T, TError>(
        this IEnumerable<Task<Result<T, TError>>> tasks)
        where TError : ResultError
    {
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Traverse();
    }
}
