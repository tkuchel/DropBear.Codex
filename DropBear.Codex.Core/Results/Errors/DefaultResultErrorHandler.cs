#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Default implementation of result error handling with enhanced diagnostics.
///     Optimized for .NET 9 with caching and telemetry.
/// </summary>
public sealed class DefaultResultErrorHandler : IResultErrorHandler
{
    // Cache for error type constructors to avoid reflection overhead
    private static readonly ConcurrentDictionary<Type, Func<string, ResultError>> ErrorFactoryCache = new();

    private static readonly ConcurrentDictionary<Type, Func<string, Exception, ResultError>>
        ErrorWithExceptionFactoryCache = new();

    private readonly bool _captureStackTraces;
    private readonly IResultTelemetry _telemetry;

    /// <summary>
    ///     Initializes a new instance of DefaultResultErrorHandler.
    /// </summary>
    /// <param name="telemetry">The telemetry instance for tracking errors.</param>
    /// <param name="captureStackTraces">Whether to capture stack traces for errors (adds overhead).</param>
    public DefaultResultErrorHandler(
        IResultTelemetry? telemetry = null,
        bool captureStackTraces = true)
    {
        _telemetry = telemetry ?? new DefaultResultTelemetry();
        _captureStackTraces = captureStackTraces;
    }

    #region Error Classification

    /// <summary>
    ///     Classifies an exception and returns appropriate error information.
    /// </summary>
    public ErrorClassification ClassifyException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OperationCanceledException => new ErrorClassification(
                ErrorCategory.Cancelled,
                ErrorSeverity.Low,
                false,
                false),

            TimeoutException => new ErrorClassification(
                ErrorCategory.Timeout,
                ErrorSeverity.Medium,
                true,
                true),

            UnauthorizedAccessException or SecurityException => new ErrorClassification(
                ErrorCategory.Authorization,
                ErrorSeverity.High,
                false,
                false),

            ArgumentException or ArgumentNullException => new ErrorClassification(
                ErrorCategory.Validation,
                ErrorSeverity.Medium,
                false,
                false),

            InvalidOperationException => new ErrorClassification(
                ErrorCategory.InvalidOperation,
                ErrorSeverity.Medium,
                false,
                false),

            IOException => new ErrorClassification(
                ErrorCategory.IO,
                ErrorSeverity.Medium,
                true,
                true),

            OutOfMemoryException or StackOverflowException => new ErrorClassification(
                ErrorCategory.Critical,
                ErrorSeverity.Critical,
                false,
                false),

            _ => new ErrorClassification(
                ErrorCategory.Unknown,
                ErrorSeverity.Medium,
                false,
                false)
        };
    }

    #endregion

    #region IResultErrorHandler Implementation

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<T, TError> HandleError<T, TError>(Exception exception)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Track the exception
        _telemetry.TrackException(exception, ResultState.Failure, typeof(Result<T, TError>));

        // Create error with context
        var error = CreateErrorFromException<TError>(exception);

        return Result<T, TError>.Failure(error, exception);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<T, TError> HandleError<T, TError>(TError error)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(error);

        return Result<T, TError>.Failure(error);
    }

    #endregion

    #region Extended Error Handling

    /// <summary>
    ///     Handles an exception and returns a result with enhanced error information.
    /// </summary>
    public Result<T, TError> HandleErrorWithContext<T, TError>(
        Exception exception,
        string context,
        IReadOnlyDictionary<string, object>? metadata = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        _telemetry.TrackException(exception, ResultState.Failure, typeof(Result<T, TError>));

        var error = CreateErrorFromException<TError>(exception);

        // Add context
        error = (TError)error.WithMetadata("Context", context);

        // Add additional metadata if provided
        if (metadata != null)
        {
            error = (TError)error.WithMetadata(metadata);
        }

        // Add stack trace if enabled
        if (_captureStackTraces && !string.IsNullOrEmpty(exception.StackTrace))
        {
            error = (TError)error.WithMetadata("StackTrace", exception.StackTrace);
        }

        return Result<T, TError>.Failure(error, exception);
    }

    /// <summary>
    ///     Handles multiple exceptions and aggregates them into a single result.
    /// </summary>
    public Result<T, TError> HandleAggregateErrors<T, TError>(
        IEnumerable<Exception> exceptions)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(exceptions);

        var exceptionList = exceptions.ToList();
        if (exceptionList.Count == 0)
        {
            throw new ArgumentException("Exception collection cannot be empty", nameof(exceptions));
        }

        // Track all exceptions
        foreach (var exception in exceptionList)
        {
            _telemetry.TrackException(exception, ResultState.Failure, typeof(Result<T, TError>));
        }

        // Create aggregate exception
        var aggregateException = exceptionList.Count == 1
            ? exceptionList[0]
            : new AggregateException("Multiple errors occurred", exceptionList);

        // Create error with count information
        var error = CreateErrorFromException<TError>(aggregateException);
        error = (TError)error.WithMetadata("ErrorCount", exceptionList.Count);
        error = (TError)error.WithMetadata("ErrorTypes",
            string.Join(", ", exceptionList.Select(e => e.GetType().Name).Distinct(StringComparer.OrdinalIgnoreCase)));

        return Result<T, TError>.Failure(error, aggregateException);
    }

    /// <summary>
    ///     Wraps an operation in try-catch and returns a Result.
    /// </summary>
    public Result<T, TError> TryExecute<T, TError>(
        Func<T> operation,
        string? context = null)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            var result = operation();
            return Result<T, TError>.Success(result);
        }
        catch (Exception ex)
        {
            return context != null
                ? HandleErrorWithContext<T, TError>(ex, context)
                : HandleError<T, TError>(ex);
        }
    }

    /// <summary>
    ///     Wraps an async operation in try-catch and returns a Result.
    /// </summary>
    public async ValueTask<Result<T, TError>> TryExecuteAsync<T, TError>(
        Func<ValueTask<T>> operation,
        string? context = null,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            var result = await operation().ConfigureAwait(false);
            return Result<T, TError>.Success(result);
        }
        catch (OperationCanceledException)
        {
            var error = CreateError<TError>("Operation was cancelled");
            error = (TError)error.WithMetadata("Cancelled", true);
            return Result<T, TError>.Cancelled(error);
        }
        catch (Exception ex)
        {
            return context != null
                ? HandleErrorWithContext<T, TError>(ex, context)
                : HandleError<T, TError>(ex);
        }
    }

    #endregion

    #region Error Creation Helpers

    /// <summary>
    ///     Creates an error from an exception with caching for performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TError CreateErrorFromException<TError>(Exception exception)
        where TError : ResultError
    {
        var factory = ErrorWithExceptionFactoryCache.GetOrAdd(
            typeof(TError),
            static type => CreateErrorWithExceptionFactory(type));

        try
        {
            return (TError)factory(exception.Message, exception);
        }
        catch
        {
            // Fallback if constructor with exception doesn't exist
            return CreateError<TError>(exception.Message);
        }
    }

    /// <summary>
    ///     Creates an error with a message.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TError CreateError<TError>(string message)
        where TError : ResultError
    {
        var factory = ErrorFactoryCache.GetOrAdd(
            typeof(TError),
            static type => CreateErrorFactory(type));

        return (TError)factory(message);
    }

    /// <summary>
    ///     Creates a cached factory for error construction with message only.
    /// </summary>
    private static Func<string, ResultError> CreateErrorFactory(Type errorType)
    {
        // Try to find constructor with string parameter
        var constructor = errorType.GetConstructor(new[] { typeof(string) });
        if (constructor == null)
        {
            throw new InvalidOperationException(
                $"Error type {errorType.Name} must have a constructor that accepts a string message.");
        }

        return message => (ResultError)constructor.Invoke(new object[] { message });
    }

    /// <summary>
    ///     Creates a cached factory for error construction with message and exception.
    /// </summary>
    private static Func<string, Exception, ResultError> CreateErrorWithExceptionFactory(Type errorType)
    {
        // Try to find constructor with string and exception parameters
        var constructor = errorType.GetConstructor(new[] { typeof(string), typeof(Exception) });
        if (constructor != null)
        {
            return (message, exception) =>
                (ResultError)constructor.Invoke(new object[] { message, exception });
        }

        // Fallback to message-only constructor
        var messageConstructor = errorType.GetConstructor(new[] { typeof(string) });
        if (messageConstructor != null)
        {
            return (message, _) =>
                (ResultError)messageConstructor.Invoke(new object[] { message });
        }

        throw new InvalidOperationException(
            $"Error type {errorType.Name} must have a constructor that accepts a string message.");
    }

    #endregion
}

#region Supporting Types

/// <summary>
///     SecurityException placeholder for .NET Standard compatibility.
/// </summary>
sealed file class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
}

#endregion
