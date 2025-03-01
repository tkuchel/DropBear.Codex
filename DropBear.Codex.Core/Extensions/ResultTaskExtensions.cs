#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides extension methods for Task-based operations with Result types.
///     Simplifies working with asynchronous Result operations and error handling.
/// </summary>
public static class ResultTaskExtensions
{
    /// <summary>
    ///     Awaits a task that returns a Result and flattens the result.
    ///     Handles task-level exceptions by converting them to a Failure result.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="resultTask">The task returning a Result to await.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    ///     A ValueTask containing the Result. If the original task fails with an exception,
    ///     returns a Failure Result with the exception details.
    /// </returns>
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
            // Create a cancelled error result for the specific error type
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

    /// <summary>
    ///     Converts a ValueTask to a Result, handling any exceptions.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    /// <typeparam name="TError">The type of the error value.</typeparam>
    /// <param name="task">The ValueTask to convert.</param>
    /// <param name="errorFactory">Factory function to create an error from an exception.</param>
    /// <returns>
    ///     A ValueTask containing a Result. Success with the task's result if successful,
    ///     or Failure with an error created by the errorFactory if an exception occurs.
    /// </returns>
    public static async ValueTask<Result<T, TError>> ToResultAsync<T, TError>(
        this ValueTask<T> task,
        Func<Exception, TError> errorFactory)
        where TError : ResultError
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            return Result<T, TError>.Success(result);
        }
        catch (OperationCanceledException ex)
        {
            return Result<T, TError>.Cancelled(errorFactory(ex));
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(errorFactory(ex), ex);
        }
    }

    /// <summary>
    ///     Transforms a successful Result into a new Result using an async mapper function.
    /// </summary>
    /// <typeparam name="T">The input success value type.</typeparam>
    /// <typeparam name="TResult">The output success value type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The original Result to transform.</param>
    /// <param name="asyncMapper">
    ///     An async function that maps the success value to a new success value.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    ///     A ValueTask containing a new Result with the transformed success value if the original
    ///     Result was successful, or a Result with the original error if it was a failure.
    /// </returns>
    public static async ValueTask<Result<TResult, TError>> MapAsync<T, TResult, TError>(
        this Result<T, TError> result,
        Func<T, CancellationToken, ValueTask<TResult>> asyncMapper,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        if (!result.IsSuccess)
        {
            return Result<TResult, TError>.Failure(result.Error!);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mappedValue = await asyncMapper(result.Value!, cancellationToken)
                .ConfigureAwait(false);
            return Result<TResult, TError>.Success(mappedValue);
        }
        catch (OperationCanceledException)
        {
            return Result<TResult, TError>.Cancelled(result.Error!);
        }
        catch (Exception ex)
        {
            return Result<TResult, TError>.Failure(result.Error!, ex);
        }
    }
}
