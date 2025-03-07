#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides utility extension methods for Result types, including
///     Unit conversions, error handling, and diagnostic capabilities.
/// </summary>
public static class UtilityExtensions
{
    #region Unit Extensions

    /// <summary>
    ///     Converts any value to Unit, effectively discarding it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Unit ToUnit<T>(this T _)
    {
        return Unit.Value;
    }

    /// <summary>
    ///     Asynchronously converts any value to Unit, effectively discarding it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ToUnitAsync<T>(this Task<T> task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }

    /// <summary>
    ///     Asynchronously converts any value to Unit, effectively discarding it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ToUnitAsync<T>(this ValueTask<T> task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }

    #endregion

    #region Error Extensions

    /// <summary>
    ///     Combines multiple errors into a single error message.
    /// </summary>
    public static TError Combine<TError>(
        this IEnumerable<TError> errors,
        string separator = "; ")
        where TError : ResultError
    {
        var messages = errors.Select(e => e.Message);
        var combinedMessage = string.Join(separator, messages);
        return (TError)Activator.CreateInstance(typeof(TError), combinedMessage)!;
    }

    /// <summary>
    ///     Creates a new error with additional context information.
    /// </summary>
    public static TError WithContext<TError>(
        this TError error,
        string context)
        where TError : ResultError
    {
        return (TError)error.WithMetadata("Context", context);
    }

    /// <summary>
    ///     Creates a new error with an associated exception.
    /// </summary>
    public static TError WithException<TError>(
        this TError error,
        Exception exception)
        where TError : ResultError
    {
        return (TError)error.WithMetadata("Exception", exception.ToString());
    }

    #endregion

    #region Diagnostic Extensions

    /// <summary>
    ///     Applies a diagnostic handler to the result, allowing inspection
    ///     of diagnostic information without modifying the result.
    /// </summary>
    public static Result<T, TError> WithDiagnostics<T, TError>(
        this Result<T, TError> result,
        Action<DiagnosticInfo> diagnosticHandler)
        where TError : ResultError
    {
        var info = (result as IResultDiagnostics)!.GetDiagnostics();
        diagnosticHandler(info);
        return result;
    }

    /// <summary>
    ///     Applies a timing handler to the result, allowing tracking of
    ///     operation timing without modifying the result.
    /// </summary>
    public static Result<T, TError> WithTiming<T, TError>(
        this Result<T, TError> result,
        Action<OperationTiming> timingHandler)
        where TError : ResultError
    {
        if (result is not ResultBase baseResult)
        {
            return result;
        }

        var creationTime = baseResult.CreatedAt; // if you stored that
        var timing = new OperationTiming(creationTime, DateTime.UtcNow);
        timingHandler(timing);
        return result;
    }

    #endregion
}
