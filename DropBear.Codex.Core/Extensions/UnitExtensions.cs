#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Extensions;

/// <summary>
///     Provides extension methods for integrating the Unit type with the Result pattern.
/// </summary>
public static class UnitExtensions
{
    /// <summary>
    ///     Converts a void-returning action to a Result with Unit value.
    /// </summary>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="action">The action to convert.</param>
    /// <param name="errorHandler">Function to create an error from an exception.</param>
    /// <returns>A Result with Unit value or an error.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Unit, TError> ToResult<TError>(
        this Action action,
        Func<Exception, TError> errorHandler)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(errorHandler);

        try
        {
            action();
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, TError>.Failure(errorHandler(ex), ex);
        }
    }

    /// <summary>
    ///     Converts a void-returning asynchronous function to a Result with Unit value.
    /// </summary>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="asyncAction">The asynchronous action to convert.</param>
    /// <param name="errorHandler">Function to create an error from an exception.</param>
    /// <returns>A task that completes with a Result containing Unit or an error.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public static async ValueTask<Result<Unit, TError>> ToResultAsync<TError>(
        this Func<Task> asyncAction,
        Func<Exception, TError> errorHandler)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        ArgumentNullException.ThrowIfNull(errorHandler);

        try
        {
            await asyncAction().ConfigureAwait(false);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, TError>.Failure(errorHandler(ex), ex);
        }
    }

    /// <summary>
    ///     Converts a void-returning ValueTask asynchronous function to a Result with Unit value.
    /// </summary>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="asyncAction">The ValueTask asynchronous action to convert.</param>
    /// <param name="errorHandler">Function to create an error from an exception.</param>
    /// <returns>A ValueTask that completes with a Result containing Unit or an error.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public static async ValueTask<Result<Unit, TError>> ToResultAsync<TError>(
        this Func<ValueTask> asyncAction,
        Func<Exception, TError> errorHandler)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        ArgumentNullException.ThrowIfNull(errorHandler);

        try
        {
            await asyncAction().ConfigureAwait(false);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, TError>.Failure(errorHandler(ex), ex);
        }
    }

    /// <summary>
    ///     Executes an action for a value in a successful result and returns Unit.
    ///     If the result is a failure, the action is not executed and the error is propagated.
    /// </summary>
    /// <typeparam name="T">The type of the value in the result.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The result containing a value or an error.</param>
    /// <param name="action">The action to execute with the value if successful.</param>
    /// <returns>A Result with Unit value or the original error.</returns>
    /// <exception cref="ArgumentNullException">Thrown if action is null.</exception>
    public static Result<Unit, TError> ForEach<T, TError>(
        this Result<T, TError> result,
        Action<T> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!result.IsSuccess)
        {
            return Result<Unit, TError>.Failure(result.Error!);
        }

        try
        {
            action(result.Value!);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, TError>.Failure(result.Error!, ex);
        }
    }

    /// <summary>
    ///     Executes an asynchronous action for a value in a successful result and returns Unit.
    ///     If the result is a failure, the action is not executed and the error is propagated.
    /// </summary>
    /// <typeparam name="T">The type of the value in the result.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The result containing a value or an error.</param>
    /// <param name="asyncAction">The asynchronous action to execute with the value if successful.</param>
    /// <returns>A ValueTask that completes with a Result containing Unit or the original error.</returns>
    /// <exception cref="ArgumentNullException">Thrown if asyncAction is null.</exception>
    public static async ValueTask<Result<Unit, TError>> ForEachAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, Task> asyncAction)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        if (!result.IsSuccess)
        {
            return Result<Unit, TError>.Failure(result.Error!);
        }

        try
        {
            await asyncAction(result.Value!).ConfigureAwait(false);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, TError>.Failure(result.Error!, ex);
        }
    }

    /// <summary>
    ///     Executes a ValueTask asynchronous action for a value in a successful result and returns Unit.
    ///     If the result is a failure, the action is not executed and the error is propagated.
    /// </summary>
    /// <typeparam name="T">The type of the value in the result.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The result containing a value or an error.</param>
    /// <param name="asyncAction">The ValueTask asynchronous action to execute with the value if successful.</param>
    /// <returns>A ValueTask that completes with a Result containing Unit or the original error.</returns>
    /// <exception cref="ArgumentNullException">Thrown if asyncAction is null.</exception>
    public static async ValueTask<Result<Unit, TError>> ForEachAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, ValueTask> asyncAction)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        if (!result.IsSuccess)
        {
            return Result<Unit, TError>.Failure(result.Error!);
        }

        try
        {
            await asyncAction(result.Value!).ConfigureAwait(false);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, TError>.Failure(result.Error!, ex);
        }
    }
}
