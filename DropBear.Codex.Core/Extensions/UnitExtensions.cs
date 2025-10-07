#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Extensions;

/// <summary>
///     Provides extension methods for integrating the Unit type with the Result pattern.
///     Optimized for .NET 9 with ValueTask patterns.
/// </summary>
public static class UnitExtensions
{
    #region Action to Result Conversions

    /// <summary>
    ///     Converts a void-returning action to a Result with Unit value.
    /// </summary>
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
    ///     Converts a void-returning Task function to a Result with Unit value.
    /// </summary>
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

    #endregion

    #region ForEach Operations

    /// <summary>
    ///     Executes an action for a value in a successful result and returns Unit.
    ///     If the result is a failure, the action is not executed and the error is propagated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Unit, TError> ForEach<T, TError>(
        this Result<T, TError> result,
        Action<T> action,
        Func<TError>? errorFactory = null,
        Func<Exception, TError>? exceptionFactory = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!result.IsSuccess)
        {
            return FailureFromResult(result, errorFactory);
        }

        try
        {
            action(result.Value!);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return FailureFromException(result, ex, errorFactory, exceptionFactory);
        }
    }

    /// <summary>
    ///     Executes an asynchronous action for a value in a successful result and returns Unit.
    /// </summary>
    public static async ValueTask<Result<Unit, TError>> ForEachAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, ValueTask> asyncAction,
        Func<TError>? errorFactory = null,
        Func<Exception, TError>? exceptionFactory = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        if (!result.IsSuccess)
        {
            return FailureFromResult(result, errorFactory);
        }

        try
        {
            await asyncAction(result.Value!).ConfigureAwait(false);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return FailureFromException(result, ex, errorFactory, exceptionFactory);
        }
    }

    /// <summary>
    ///     Executes a Task asynchronous action for a value in a successful result and returns Unit.
    /// </summary>
    public static async ValueTask<Result<Unit, TError>> ForEachAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, Task> asyncAction,
        Func<TError>? errorFactory = null,
        Func<Exception, TError>? exceptionFactory = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        if (!result.IsSuccess)
        {
            return FailureFromResult(result, errorFactory);
        }

        try
        {
            await asyncAction(result.Value!).ConfigureAwait(false);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return FailureFromException(result, ex, errorFactory, exceptionFactory);
        }
    }

    #endregion

    #region Value Discarding

    /// <summary>
    ///     Discards the value of a successful result, converting it to a Unit result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Unit, TError> ToUnit<T, TError>(
        this Result<T, TError> result,
        Func<TError>? errorFactory = null)
        where TError : ResultError
    {
        return result.IsSuccess
            ? Result<Unit, TError>.Success(Unit.Value)
            : FailureFromResult(result, errorFactory);
    }

    /// <summary>
    ///     Discards the value of a successful result after executing an action.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Unit, TError> ToUnit<T, TError>(
        this Result<T, TError> result,
        Action<T> onSuccess,
        Func<TError>? errorFactory = null,
        Func<Exception, TError>? exceptionFactory = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (result.IsSuccess)
        {
            try
            {
                onSuccess(result.Value!);
            }
            catch (Exception ex)
            {
                return FailureFromException(result, ex, errorFactory, exceptionFactory);
            }
        }

        return result.ToUnit(errorFactory);
    }

    /// <summary>
    ///     Discards the value of a successful result after executing an async action.
    /// </summary>
    public static async ValueTask<Result<Unit, TError>> ToUnitAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, ValueTask> onSuccess,
        Func<TError>? errorFactory = null,
        Func<Exception, TError>? exceptionFactory = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (result.IsSuccess)
        {
            try
            {
                await onSuccess(result.Value!).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return FailureFromException(result, ex, errorFactory, exceptionFactory);
            }
        }

        return result.ToUnit(errorFactory);
    }

    #endregion

    #region Collection Operations

    /// <summary>
    ///     Executes an action for each item in a collection result, returning Unit.
    /// </summary>
    public static Result<Unit, TError> ForEachItem<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Action<T> action,
        Func<TError>? errorFactory = null,
        Func<Exception, TError>? exceptionFactory = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!result.IsSuccess)
        {
            return FailureFromResult(result, errorFactory);
        }

        try
        {
            foreach (var item in result.Value!)
            {
                action(item);
            }

            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return FailureFromException(result, ex, errorFactory, exceptionFactory);
        }
    }

    /// <summary>
    ///     Executes an async action for each item in a collection result, returning Unit.
    /// </summary>
    public static async ValueTask<Result<Unit, TError>> ForEachItemAsync<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, ValueTask> asyncAction,
        Func<TError>? errorFactory = null,
        Func<Exception, TError>? exceptionFactory = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        if (!result.IsSuccess)
        {
            return FailureFromResult(result, errorFactory);
        }

        try
        {
            foreach (var item in result.Value!)
            {
                await asyncAction(item).ConfigureAwait(false);
            }

            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return FailureFromException(result, ex, errorFactory, exceptionFactory);
        }
    }

    #endregion

    #region Helpers

    private static Result<Unit, TError> FailureFromResult<T, TError>(
        Result<T, TError> result,
        Func<TError>? errorFactory)
        where TError : ResultError
    {
        var error = result.Error
                    ?? errorFactory?.Invoke()
                    ?? (TryCreateSimpleError("Operation failed.", result.Exception, out TError fallback)
                        ? fallback
                        : throw new InvalidOperationException(
                            $"Unable to create an instance of {typeof(TError).FullName}. Provide an errorFactory delegate when calling this method."));

        return Result<Unit, TError>.Failure(error, result.Exception);
    }

    private static Result<Unit, TError> FailureFromException<T, TError>(
        Result<T, TError> result,
        Exception exception,
        Func<TError>? errorFactory,
        Func<Exception, TError>? exceptionFactory)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(exception);

        var error = exceptionFactory?.Invoke(exception)
                    ?? result.Error
                    ?? errorFactory?.Invoke()
                    ?? (TryCreateSimpleError($"Operation failed: {exception.Message}", exception, out TError fallback)
                        ? fallback
                        : throw new InvalidOperationException(
                            $"Unable to create an instance of {typeof(TError).FullName}. Provide an exceptionFactory delegate when calling this method."));

        return Result<Unit, TError>.Failure(error, exception);
    }

    private static bool TryCreateSimpleError<TError>(string message, Exception? exception, out TError error)
        where TError : ResultError
    {
        if (typeof(TError).IsAssignableFrom(typeof(SimpleError)))
        {
            var fallback = exception is null
                ? SimpleError.Create(message)
                : SimpleError.FromException(exception);

            error = (TError)(ResultError)fallback;
            return true;
        }

        error = null!;
        return false;
    }

    #endregion
}
