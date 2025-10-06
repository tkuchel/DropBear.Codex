#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Utility extensions for Result types providing additional helper functionality.
///     Optimized for .NET 9 with modern patterns.
/// </summary>
public static class UtilityExtensions
{
    #region Exception Handling

    /// <summary>
    ///     Executes an action if the result contains an exception.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> OnException<T, TError>(
        this Result<T, TError> result,
        Action<Exception> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);

        if (result.Exception is not null)
        {
            action(result.Exception);
        }

        return result;
    }

    #endregion

    #region Result Chaining

    /// <summary>
    ///     Chains multiple operations, stopping at the first failure.
    /// </summary>
    public static Result<T, TError> Chain<T, TError>(
        this Result<T, TError> result,
        params Func<T, Result<T, TError>>[] operations)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(operations);

        if (!result.IsSuccess)
        {
            return result;
        }

        var current = result;

        foreach (var operation in operations)
        {
            if (!current.IsSuccess)
            {
                return current;
            }

            current = operation(current.Value!);
        }

        return current;
    }

    #endregion

    #region Timing and Performance

    /// <summary>
    ///     Executes a function and captures its execution time.
    /// </summary>
    public static Result<(T Value, TimeSpan Duration), TError> WithTiming<T, TError>(
        this Func<Result<T, TError>> operation)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);

        var stopwatch = Stopwatch.StartNew();
        var result = operation();
        stopwatch.Stop();

        return result.IsSuccess
            ? Result<(T, TimeSpan), TError>.Success((result.Value!, stopwatch.Elapsed))
            : Result<(T, TimeSpan), TError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Executes an async function and captures its execution time.
    /// </summary>
    public static async ValueTask<Result<(T Value, TimeSpan Duration), TError>> WithTimingAsync<T, TError>(
        this Func<ValueTask<Result<T, TError>>> operationAsync)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operationAsync);

        var stopwatch = Stopwatch.StartNew();
        var result = await operationAsync().ConfigureAwait(false);
        stopwatch.Stop();

        return result.IsSuccess
            ? Result<(T, TimeSpan), TError>.Success((result.Value!, stopwatch.Elapsed))
            : Result<(T, TimeSpan), TError>.Failure(result.Error!);
    }

    /// <summary>
    ///     Adds execution metadata to a result.
    /// </summary>
    public static Result<T, TError> WithMetadata<T, TError>(
        this Result<T, TError> result,
        string key,
        object value)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        if (result.Error is not null)
        {
            var updatedError = result.Error.WithMetadata(key, value);
            return result.IsSuccess
                ? Result<T, TError>.Success(result.Value!)
                : Result<T, TError>.Failure((TError)updatedError);
        }

        return result;
    }

    #endregion

    #region Timeout Extensions

    /// <summary>
    ///     Executes an async operation with a timeout.
    /// </summary>
    public static async ValueTask<Result<T, TError>> WithTimeout<T, TError>(
        this Func<CancellationToken, ValueTask<Result<T, TError>>> operationAsync,
        TimeSpan timeout)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operationAsync);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");
        }

        using var cts = new CancellationTokenSource(timeout);

        try
        {
            return await operationAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            var error = (TError)Activator.CreateInstance(
                typeof(TError),
                $"Operation timed out after {timeout.TotalSeconds:F2} seconds")!;
            return Result<T, TError>.Failure(error);
        }
    }

    /// <summary>
    ///     Executes an async operation with a timeout and default value on timeout.
    /// </summary>
    public static async ValueTask<Result<T, TError>> WithTimeoutOrDefault<T, TError>(
        this Func<CancellationToken, ValueTask<Result<T, TError>>> operationAsync,
        TimeSpan timeout,
        T defaultValue)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operationAsync);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");
        }

        using var cts = new CancellationTokenSource(timeout);

        try
        {
            return await operationAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            // Return default value on timeout
            return Result<T, TError>.Success(defaultValue);
        }
    }

    #endregion

    #region Conditional Execution

    /// <summary>
    ///     Executes an action only if the result is successful.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> OnSuccess<T, TError>(
        this Result<T, TError> result,
        Action<T> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);

        if (result.IsSuccess && result.Value is not null)
        {
            action(result.Value);
        }

        return result;
    }

    /// <summary>
    ///     Executes an async action only if the result is successful.
    /// </summary>
    public static async ValueTask<Result<T, TError>> OnSuccessAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, ValueTask> actionAsync)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(actionAsync);

        if (result.IsSuccess && result.Value is not null)
        {
            await actionAsync(result.Value).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    ///     Executes an action only if the result is a failure.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> OnFailure<T, TError>(
        this Result<T, TError> result,
        Action<TError> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);

        if (!result.IsSuccess && result.Error is not null)
        {
            action(result.Error);
        }

        return result;
    }

    /// <summary>
    ///     Executes an async action only if the result is a failure.
    /// </summary>
    public static async ValueTask<Result<T, TError>> OnFailureAsync<T, TError>(
        this Result<T, TError> result,
        Func<TError, ValueTask> actionAsync)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(actionAsync);

        if (!result.IsSuccess && result.Error is not null)
        {
            await actionAsync(result.Error).ConfigureAwait(false);
        }

        return result;
    }

    #endregion

    #region Debugging and Diagnostics

    /// <summary>
    ///     Executes a side effect with the entire RESULT object.
    ///     The action receives the complete result (including state, error, etc.).
    ///     Use this when you need access to the full result metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// result.Tap(r => Logger.Log($"Result state: {r.State}"));
    /// </code>
    /// </example>
    public static Result<T, TError> Tap<T, TError>(
        this Result<T, TError> result,
        Action<Result<T, TError>> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);

        action(result);
        return result;
    }

    /// <summary>
    ///     Taps into the result asynchronously for debugging.
    /// </summary>
    public static async ValueTask<Result<T, TError>> TapAsync<T, TError>(
        this Result<T, TError> result,
        Func<Result<T, TError>, ValueTask> actionAsync)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(actionAsync);

        await actionAsync(result).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    ///     Logs the result to console (useful for debugging).
    /// </summary>
    public static Result<T, TError> Log<T, TError>(
        this Result<T, TError> result,
        string? prefix = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);

        var message = prefix is not null ? $"{prefix}: " : string.Empty;

        if (result.IsSuccess)
        {
            Console.WriteLine($"{message}Success - Value: {result.Value}");
        }
        else
        {
            Console.WriteLine($"{message}Failure - Error: {result.Error?.Message ?? "Unknown"}");
            if (result.Exception is not null)
            {
                Console.WriteLine($"{message}Exception: {result.Exception.Message}");
            }
        }

        return result;
    }

    #endregion

    #region Value Extraction

    /// <summary>
    ///     Gets the value or executes a fallback function.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ValueOr<T, TError>(
        this Result<T, TError> result,
        Func<TError, T> fallback)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(fallback);

        return result.IsSuccess && result.Value is not null
            ? result.Value
            : fallback(result.Error!);
    }

    /// <summary>
    ///     Gets the value or executes an async fallback function.
    /// </summary>
    public static async ValueTask<T> ValueOrAsync<T, TError>(
        this Result<T, TError> result,
        Func<TError, ValueTask<T>> fallbackAsync)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(fallbackAsync);

        return result.IsSuccess && result.Value is not null
            ? result.Value
            : await fallbackAsync(result.Error!).ConfigureAwait(false);
    }

    #endregion

    #region Type Conversion

    /// <summary>
    ///     Converts a Result to a nullable value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? ToNullable<T, TError>(this Result<T, TError> result)
        where TError : ResultError
        where T : struct =>
        result.IsSuccess ? result.Value : null;

    /// <summary>
    ///     Converts a nullable value to a Result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> ToResult<T, TError>(
        this T? nullable,
        TError error)
        where TError : ResultError
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(error);

        return nullable.HasValue
            ? Result<T, TError>.Success(nullable.Value)
            : Result<T, TError>.Failure(error);
    }

    /// <summary>
    ///     Converts a nullable reference type to a Result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> ToResult<T, TError>(
        this T? value,
        TError error)
        where TError : ResultError
        where T : class
    {
        ArgumentNullException.ThrowIfNull(error);

        return value is not null
            ? Result<T, TError>.Success(value)
            : Result<T, TError>.Failure(error);
    }

    #endregion
}
