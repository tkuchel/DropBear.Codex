#region

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

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
        Action<T> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!result.IsSuccess)
        {
            return FailureFromResult(result);
        }

        try
        {
            action(result.Value!);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return FailureFromException(result, ex);
        }
    }

    /// <summary>
    ///     Executes an asynchronous action for a value in a successful result and returns Unit.
    /// </summary>
    public static async ValueTask<Result<Unit, TError>> ForEachAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, ValueTask> asyncAction)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        if (!result.IsSuccess)
        {
            return FailureFromResult(result);
        }

        try
        {
            await asyncAction(result.Value!).ConfigureAwait(false);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return FailureFromException(result, ex);
        }
    }

    /// <summary>
    ///     Executes a Task asynchronous action for a value in a successful result and returns Unit.
    /// </summary>
    public static async ValueTask<Result<Unit, TError>> ForEachAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, Task> asyncAction)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        if (!result.IsSuccess)
        {
            return FailureFromResult(result);
        }

        try
        {
            await asyncAction(result.Value!).ConfigureAwait(false);
            return Result<Unit, TError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return FailureFromException(result, ex);
        }
    }

    #endregion

    #region Value Discarding

    /// <summary>
    ///     Discards the value of a successful result, converting it to a Unit result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Unit, TError> ToUnit<T, TError>(
        this Result<T, TError> result)
        where TError : ResultError
    {
        return result.IsSuccess
            ? Result<Unit, TError>.Success(Unit.Value)
            : FailureFromResult(result);
    }

    /// <summary>
    ///     Discards the value of a successful result after executing an action.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Unit, TError> ToUnit<T, TError>(
        this Result<T, TError> result,
        Action<T> onSuccess)
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
                return FailureFromException(result, ex);
            }
        }

        return result.ToUnit();
    }

    /// <summary>
    ///     Discards the value of a successful result after executing an async action.
    /// </summary>
    public static async ValueTask<Result<Unit, TError>> ToUnitAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, ValueTask> onSuccess)
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
                return FailureFromException(result, ex);
            }
        }

        return result.ToUnit();
    }

    #endregion

    #region Collection Operations

    /// <summary>
    ///     Executes an action for each item in a collection result, returning Unit.
    /// </summary>
    public static Result<Unit, TError> ForEachItem<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Action<T> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!result.IsSuccess)
        {
            return FailureFromResult(result);
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
            return FailureFromException(result, ex);
        }
    }

    /// <summary>
    ///     Executes an async action for each item in a collection result, returning Unit.
    /// </summary>
    public static async ValueTask<Result<Unit, TError>> ForEachItemAsync<T, TError>(
        this Result<IEnumerable<T>, TError> result,
        Func<T, ValueTask> asyncAction)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(asyncAction);

        if (!result.IsSuccess)
        {
            return FailureFromResult(result);
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
            return FailureFromException(result, ex);
        }
    }

    #endregion

    #region Helpers

    private static Result<Unit, TError> FailureFromResult<T, TError>(Result<T, TError> result)
        where TError : ResultError
    {
        var error = result.Error ?? CreateDefaultError<TError>();
        return Result<Unit, TError>.Failure(error, result.Exception);
    }

    private static Result<Unit, TError> FailureFromException<T, TError>(Result<T, TError> result, Exception exception)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(exception);

        var error = result.Error ?? CreateErrorFromException<TError>(exception);
        return Result<Unit, TError>.Failure(error, exception);
    }

    private static TError CreateErrorFromException<TError>(Exception exception)
        where TError : ResultError
    {
        try
        {
            return (TError)Activator.CreateInstance(
                typeof(TError),
                $"Operation failed: {exception.Message}")!;
        }
        catch
        {
            return CreateDefaultError<TError>();
        }
    }

    private static TError CreateDefaultError<TError>()
        where TError : ResultError
    {
        var type = typeof(TError);

        try
        {
            return (TError)Activator.CreateInstance(type, "Operation failed.")!;
        }
        catch
        {
            try
            {
                return (TError)Activator.CreateInstance(type)!;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Unable to create an instance of {type.FullName}. Ensure it has a parameterless or string constructor.",
                    ex);
            }
        }
    }

    #endregion
}
