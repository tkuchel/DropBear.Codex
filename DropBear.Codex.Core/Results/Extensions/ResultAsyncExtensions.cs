#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides asynchronous extension methods for <see cref="Result{T,TError}" /> types.
/// </summary>
public static class ResultAsyncExtensions
{
    /// <summary>
    ///     Asynchronously maps the value of a successful <see cref="Result{T, TError}" /> to a new type
    ///     <typeparamref name="TResult" />.
    ///     Returns a failure result if the original result is not successful.
    /// </summary>
    /// <typeparam name="T">The original result value type.</typeparam>
    /// <typeparam name="TResult">The mapped result value type.</typeparam>
    /// <typeparam name="TError">The error type, inheriting from <see cref="ResultError" />.</typeparam>
    /// <param name="result">The current <see cref="Result{T, TError}" />.</param>
    /// <param name="mapper">
    ///     A function to map the existing value <typeparamref name="T" /> to <typeparamref name="TResult" /> asynchronously.
    /// </param>
    /// <returns>A new <see cref="Result{TResult, TError}" /> containing the mapped value or the original error.</returns>
    public static async Task<Result<TResult, TError>> MapAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, Task<TResult>> mapper)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<TResult, TError>.Failure(result.Error!);
        }

        if (result.Value == null)
        {
            return Result<TResult, TError>.Success(default!);
        }

        var mappedValue = await mapper(result.Value).ConfigureAwait(false);
        return Result<TResult, TError>.Success(mappedValue);
    }

    /// <summary>
    ///     Asynchronously binds (flattens) the value of a successful <see cref="Result{T, TError}" />
    ///     into a new <see cref="Result{TResult, TError}" /> via <paramref name="binder" />.
    ///     Returns a failure result if the original result is not successful.
    /// </summary>
    /// <typeparam name="T">The original result value type.</typeparam>
    /// <typeparam name="TResult">The type of the new result value.</typeparam>
    /// <typeparam name="TError">The error type, inheriting from <see cref="ResultError" />.</typeparam>
    /// <param name="result">The current <see cref="Result{T, TError}" />.</param>
    /// <param name="binder">
    ///     A function that takes the existing value <typeparamref name="T" /> and returns a new
    ///     <see cref="Result{TResult, TError}" /> asynchronously.
    /// </param>
    /// <returns>
    ///     A new <see cref="Result{TResult, TError}" /> or the original failure if <paramref name="result" /> was not
    ///     successful.
    /// </returns>
    public static async Task<Result<TResult, TError>> BindAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, Task<Result<TResult, TError>>> binder)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<TResult, TError>.Failure(result.Error!);
        }

        if (result.Value != null)
        {
            return await binder(result.Value).ConfigureAwait(false);
        }

        return Result<TResult, TError>.Success(default!);
    }

    /// <summary>
    ///     Awaits a <see cref="Task{TResult}" /> that produces a <see cref="Result{T, TError}" />
    ///     but times out after <paramref name="timeout" /> if not completed.
    ///     Returns a <see cref="TimeoutError" /> if it exceeds the timeout.
    /// </summary>
    /// <typeparam name="T">The success value type in the result.</typeparam>
    /// <typeparam name="TError">The error type, inheriting from <see cref="ResultError" />.</typeparam>
    /// <param name="task">The asynchronous operation returning <see cref="Result{T, TError}" />.</param>
    /// <param name="timeout">A <see cref="TimeSpan" /> specifying the maximum wait time.</param>
    /// <returns>
    ///     The completed result if within the timeout; otherwise a failure with <see cref="TimeoutError" />.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if <see cref="TimeoutError" /> cannot be converted to <typeparamref name="TError" />.
    /// </exception>
    public static async Task<Result<T, TError>> TimeoutAfter<T, TError>(
        this Task<Result<T, TError>> task,
        TimeSpan timeout)
        where TError : ResultError
    {
        using var cts = new CancellationTokenSource();
        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token)).ConfigureAwait(false);

        if (completedTask == task)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        try
        {
            // Cancel the original task's token
            await cts.CancelAsync().ConfigureAwait(false);
            await task.ConfigureAwait(false); // Let the task handle cleanup
        }
        catch
        {
            // Ignore exceptions caused by cancellation
        }

        var timeoutError = new TimeoutError(timeout);
        if (timeoutError is TError typedError)
        {
            return Result<T, TError>.Failure(typedError);
        }

        throw new InvalidOperationException($"Cannot convert TimeoutError to {typeof(TError).Name}");
    }

    /// <summary>
    ///     Retries an asynchronous operation a certain number of times (<paramref name="maxAttempts" />),
    ///     optionally delaying between attempts. If all attempts fail, returns the mapped error from
    ///     <paramref name="errorMapper" />.
    /// </summary>
    /// <typeparam name="T">The success type of the <see cref="Result{T, TError}" />.</typeparam>
    /// <typeparam name="TError">The error type, inheriting from <see cref="ResultError" />.</typeparam>
    /// <param name="operation">A function that performs the asynchronous operation.</param>
    /// <param name="maxAttempts">The maximum number of attempts to make.</param>
    /// <param name="delay">A <see cref="TimeSpan" /> to wait between attempts.</param>
    /// <param name="errorMapper">A function that maps an <see cref="Exception" /> to <typeparamref name="TError" />.</param>
    /// <returns>The first successful result or a final failure after <paramref name="maxAttempts" /> attempts.</returns>
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

        // If we exit the loop in a normal way (unlikely), just map an error
        return Result<T, TError>.Failure(
            errorMapper(new InvalidOperationException($"Operation failed after {maxAttempts} attempts")));
    }

    /// <summary>
    ///     Converts a <see cref="Task{T}" /> into a <see cref="Result{T, TError}" />.
    ///     If the task throws an exception, maps it to <typeparamref name="TError" />.
    /// </summary>
    /// <typeparam name="T">The success type to return in the result.</typeparam>
    /// <typeparam name="TError">The error type, inheriting from <see cref="ResultError" />.</typeparam>
    /// <param name="task">A task returning a value of type <typeparamref name="T" />.</param>
    /// <param name="errorMapper">A function that maps an exception to <typeparamref name="TError" />.</param>
    /// <returns>A success result if the task completes, or a failure result if an exception is thrown.</returns>
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

    /// <summary>
    ///     Awaits all tasks returning <see cref="Result{T, TError}" /> in parallel, then combines them into a single
    ///     <see cref="Result{IReadOnlyList{T}, TError}" />. If any fail, returns a partial or full failure.
    /// </summary>
    /// <typeparam name="T">The result value type of each task.</typeparam>
    /// <typeparam name="TError">The error type, inheriting from <see cref="ResultError" />.</typeparam>
    /// <param name="tasks">A collection of tasks returning <see cref="Result{T, TError}" />.</param>
    /// <returns>A combined <see cref="Result{IReadOnlyList{T}, TError}" /> with aggregated success or errors.</returns>
    public static async Task<Result<IReadOnlyList<T>, TError>> WhenAll<T, TError>(
        this IEnumerable<Task<Result<T, TError>>> tasks)
        where TError : ResultError
    {
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Traverse();
    }
}
