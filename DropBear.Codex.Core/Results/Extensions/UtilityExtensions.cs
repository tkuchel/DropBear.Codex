#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides utility extension methods for Result types, including
///     diagnostics, error handling, and helper operations.
///     Optimized for .NET 9.
/// </summary>
public static class UtilityExtensions
{
    #region Error Extensions

    /// <summary>
    ///     Combines multiple errors into a single error message.
    /// </summary>
    public static TError Combine<TError>(
        this IEnumerable<TError> errors,
        string separator = "; ")
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(separator);

        var errorList = errors.ToList();
        if (errorList.Count == 0)
        {
            throw new ArgumentException("Cannot combine empty error collection", nameof(errors));
        }

        if (errorList.Count == 1)
        {
            return errorList[0];
        }

        var messages = errorList.Select(e => e.Message);
        var combinedMessage = string.Join(separator, messages);
        return (TError)Activator.CreateInstance(typeof(TError), combinedMessage)!;
    }

    /// <summary>
    ///     Creates a new error with additional context information.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TError WithContext<TError>(
        this TError error,
        string context)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        return (TError)error.WithMetadata("Context", context);
    }

    /// <summary>
    ///     Creates a new error with an associated exception in metadata.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TError WithException<TError>(
        this TError error,
        Exception exception)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(exception);

        return (TError)error.WithMetadata("Exception", exception.ToString());
    }

    /// <summary>
    ///     Creates a new error with a correlation ID for tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TError WithCorrelationId<TError>(
        this TError error,
        string correlationId)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return (TError)error.WithMetadata("CorrelationId", correlationId);
    }

    /// <summary>
    ///     Creates a new error with source information.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TError WithSource<TError>(
        this TError error,
        string source)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return (TError)error.WithMetadata("Source", source);
    }

    #endregion

    #region Diagnostic Extensions

    /// <summary>
    ///     Applies a diagnostic handler to the result, allowing inspection
    ///     of diagnostic information without modifying the result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> WithDiagnostics<T, TError>(
        this Result<T, TError> result,
        Action<DiagnosticInfo> diagnosticHandler)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(diagnosticHandler);

        if (result is IResultDiagnostics diagnostics)
        {
            var info = diagnostics.GetDiagnostics();
            diagnosticHandler(info);
        }

        return result;
    }

    /// <summary>
    ///     Applies a timing handler to the result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> WithTiming<T, TError>(
        this Result<T, TError> result,
        Action<OperationTiming> timingHandler)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(timingHandler);

        if (result is ResultBase baseResult)
        {
            var timing = new OperationTiming(baseResult.CreatedAt, DateTime.UtcNow);
            timingHandler(timing);
        }

        return result;
    }

    /// <summary>
    ///     Gets performance metrics for a result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ResultPerformanceMetrics? GetPerformanceMetrics<T, TError>(
        this Result<T, TError> result)
        where TError : ResultError
    {
        return result is ResultBase baseResult ? baseResult.GetPerformanceMetrics() : null;
    }

    /// <summary>
    ///     Gets the trace context for a result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ActivityContext? GetTraceContext<T, TError>(
        this Result<T, TError> result)
        where TError : ResultError
    {
        return result is IResultDiagnostics diagnostics ? diagnostics.GetTraceContext() : null;
    }

    #endregion

    #region Conditional Operations

    /// <summary>
    ///     Executes an action if the result is successful.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> OnSuccess<T, TError>(
        this Result<T, TError> result,
        Action<T> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(action);

        if (result.IsSuccess)
        {
            try
            {
                action(result.Value!);
            }
            catch (Exception ex)
            {
                // Log but don't change result
                Debug.WriteLine($"Exception in OnSuccess: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    ///     Executes an action if the result is a failure.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> OnFailure<T, TError>(
        this Result<T, TError> result,
        Action<TError> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!result.IsSuccess && result.Error != null)
        {
            try
            {
                action(result.Error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in OnFailure: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    ///     Executes an async action if the result is successful.
    /// </summary>
    public static async ValueTask<Result<T, TError>> OnSuccessAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, ValueTask> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(action);

        if (result.IsSuccess)
        {
            try
            {
                await action(result.Value!).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in OnSuccessAsync: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    ///     Executes an async action if the result is a failure.
    /// </summary>
    public static async ValueTask<Result<T, TError>> OnFailureAsync<T, TError>(
        this Result<T, TError> result,
        Func<TError, ValueTask> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!result.IsSuccess && result.Error != null)
        {
            try
            {
                await action(result.Error).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in OnFailureAsync: {ex.Message}");
            }
        }

        return result;
    }

    #endregion

    #region Logging Extensions

    /// <summary>
    ///     Logs the result state using the provided logger action.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Log<T, TError>(
        this Result<T, TError> result,
        Action<string> logger)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(logger);

        var message = result.IsSuccess
            ? $"Result succeeded with value: {result.Value}"
            : $"Result failed with error: {result.Error?.Message}";

        logger(message);
        return result;
    }

    /// <summary>
    ///     Logs only failures using the provided logger action.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> LogFailure<T, TError>(
        this Result<T, TError> result,
        Action<TError> logger)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!result.IsSuccess && result.Error != null)
        {
            logger(result.Error);
        }

        return result;
    }

    /// <summary>
    ///     Logs the result with detailed diagnostic information.
    /// </summary>
    public static Result<T, TError> LogDetailed<T, TError>(
        this Result<T, TError> result,
        Action<string> logger)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(logger);

        var metrics = result.GetPerformanceMetrics();
        var message = result.IsSuccess
            ? $"Result succeeded: Value={result.Value}, Elapsed={metrics.ExecutionTime.TotalMilliseconds:F2}ms"
            : $"Result failed: Error={result.Error?.Message}, Elapsed={metrics.ExecutionTime.TotalMilliseconds:F2}ms";

        logger(message);
        return result;
    }

    #endregion

    #region Assertion Extensions

    /// <summary>
    ///     Asserts that the result is successful, throwing if not.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AssertSuccess<T, TError>(
        this Result<T, TError> result,
        string? message = null)
        where TError : ResultError
    {
        if (result.IsSuccess)
        {
            return result.Value!;
        }

        var errorMessage = message ?? $"Expected success but got failure: {result.Error?.Message}";
        throw new InvalidOperationException(errorMessage);
    }

    /// <summary>
    ///     Asserts that the result is a failure, throwing if not.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TError AssertFailure<T, TError>(
        this Result<T, TError> result,
        string? message = null)
        where TError : ResultError
    {
        if (!result.IsSuccess && result.Error != null)
        {
            return result.Error;
        }

        var errorMessage = message ?? "Expected failure but got success";
        throw new InvalidOperationException(errorMessage);
    }

    #endregion

    #region Conversion Helpers

    /// <summary>
    ///     Converts a result to an Option/Maybe pattern (null on failure).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? ToNullable<T, TError>(
        this Result<T, TError> result)
        where TError : ResultError
        where T : struct
    {
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    ///     Converts a result to a tuple of (success, value, error).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (bool Success, T? Value, TError? Error) ToTuple<T, TError>(
        this Result<T, TError> result)
        where TError : ResultError
    {
        return (result.IsSuccess, result.Value, result.Error);
    }

    /// <summary>
    ///     Deconstructs a result into its components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deconstruct<T, TError>(
        this Result<T, TError> result,
        out bool success,
        out T? value,
        out TError? error)
        where TError : ResultError
    {
        success = result.IsSuccess;
        value = result.Value;
        error = result.Error;
    }

    #endregion

    #region Collection Helpers

    /// <summary>
    ///     Filters out failed results from a collection, returning only successful values.
    /// </summary>
    public static IEnumerable<T> SuccessValues<T, TError>(
        this IEnumerable<Result<T, TError>> results)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(results);

        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                yield return result.Value!;
            }
        }
    }

    /// <summary>
    ///     Filters out successful results from a collection, returning only errors.
    /// </summary>
    public static IEnumerable<TError> Errors<T, TError>(
        this IEnumerable<Result<T, TError>> results)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(results);

        foreach (var result in results)
        {
            if (!result.IsSuccess && result.Error != null)
            {
                yield return result.Error;
            }
        }
    }

    /// <summary>
    ///     Partitions results into successful values and errors.
    /// </summary>
    public static (List<T> Successes, List<TError> Errors) Partition<T, TError>(
        this IEnumerable<Result<T, TError>> results)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(results);

        var successes = new List<T>();
        var errors = new List<TError>();

        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                successes.Add(result.Value!);
            }
            else if (result.Error != null)
            {
                errors.Add(result.Error);
            }
        }

        return (successes, errors);
    }

    #endregion

    #region Timeout Extensions

    /// <summary>
    ///     Executes an operation with a timeout, returning a cancelled result if timeout occurs.
    /// </summary>
    public static async ValueTask<Result<T, TError>> WithTimeoutAsync<T, TError>(
        this Func<CancellationToken, ValueTask<Result<T, TError>>> operation,
        TimeSpan timeout)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        using var cts = new CancellationTokenSource(timeout);

        try
        {
            return await operation(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var error = ResultError.CreateTimeout<TError>(timeout);
            return Result<T, TError>.Cancelled(error);
        }
    }

    #endregion
}
