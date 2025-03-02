#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Extensions;

/// <summary>
///     Provides high-performance extension methods for creating and composing Result pipelines.
///     These extensions optimize performance by using aggressive inlining and minimizing allocations.
/// </summary>
public static class ResultPipelineExtensions
{
    /// <summary>
    ///     Chains a transformation function onto a successful result.
    ///     Similar to Map but with optimized performance and method naming aligned with LINQ-style pipelines.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">The transformation function.</param>
    /// <returns>A new result containing the transformed value or the original error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult, TError> Then<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, TResult> map)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<TResult, TError>.Failure(result.Error!);
        }

        try
        {
            return Result<TResult, TError>.Success(map(result.Value!));
        }
        catch (Exception ex)
        {
            return Result<TResult, TError>.Failure(result.Error!, ex);
        }
    }

    /// <summary>
    ///     Chains an asynchronous transformation function onto a successful result.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="mapAsync">The asynchronous transformation function.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns a new result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Result<TResult, TError>> ThenAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, CancellationToken, ValueTask<TResult>> mapAsync,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<TResult, TError>.Failure(result.Error!);
        }

        try
        {
            var transformed = await mapAsync(result.Value!, cancellationToken).ConfigureAwait(false);
            return Result<TResult, TError>.Success(transformed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TResult, TError>.Failure(result.Error!, ex);
        }
    }

    /// <summary>
    ///     Adds retry capabilities for transient errors.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The result to potentially retry.</param>
    /// <param name="isTransientError">Function that determines if the error is transient.</param>
    /// <param name="retryFunc">Function to execute for retry attempts.</param>
    /// <param name="maxRetries">Maximum number of retries.</param>
    /// <param name="initialDelay">Initial delay between retries (will be doubled each retry).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns the final result.</returns>
    public static async ValueTask<Result<T, TError>> RetryAsync<T, TError>(
        this Result<T, TError> result,
        Func<TError, bool> isTransientError,
        Func<TError, CancellationToken, ValueTask<Result<T, TError>>> retryFunc,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        if (result.IsSuccess || !isTransientError(result.Error!))
        {
            return result;
        }

        var delay = initialDelay ?? TimeSpan.FromMilliseconds(200);
        var retryAttempt = 0;
        var currentResult = result;

        while (retryAttempt < maxRetries && !currentResult.IsSuccess && isTransientError(currentResult.Error!))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            currentResult = await retryFunc(currentResult.Error!, cancellationToken).ConfigureAwait(false);
            retryAttempt++;

            // Exponential backoff with jitter for distributed systems
            var jitter = Random.Shared.Next(-50, 50);
            delay = TimeSpan.FromMilliseconds((delay.TotalMilliseconds * 2) + jitter);
        }

        return currentResult;
    }

    /// <summary>
    ///     Executes an action on success and returns the original result.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The action to execute if successful.</param>
    /// <returns>The original result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Tap<T, TError>(
        this Result<T, TError> result,
        Action<T> action)
        where TError : ResultError
    {
        if (result.IsSuccess)
        {
            try
            {
                action(result.Value!);
            }
            catch
            {
                // Ignore exceptions in tap
            }
        }

        return result;
    }

    /// <summary>
    ///     Asynchronously executes an action on success and returns the original result.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="actionAsync">The async action to execute if successful.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that returns the original result.</returns>
    public static async ValueTask<Result<T, TError>> TapAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, CancellationToken, ValueTask> actionAsync,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        if (result.IsSuccess)
        {
            try
            {
                await actionAsync(result.Value!, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore exceptions in tap
            }
        }

        return result;
    }
}
