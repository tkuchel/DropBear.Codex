#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Retry;
using Serilog;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Provides extension methods for Result types
/// </summary>
public static class ResultExtensions
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<Result<ResultError>>();

    #region Success Callbacks

    /// <summary>
    ///     Executes an action if the result is successful
    /// </summary>
    public static Result<T, TError> OnSuccess<T, TError>(
        this Result<T, TError> result,
        Action<T> action)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);

        if (result.IsSuccess)
        {
            try
            {
                action(result.Value!);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception in OnSuccess callback");
            }
        }

        return result;
    }

    /// <summary>
    ///     Executes an async action if the result is successful
    /// </summary>
    public static async ValueTask<Result<T, TError>> OnSuccessAsync<T, TError>(
        this Result<T, TError> result,
        Func<T, ValueTask> action,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(action);

        if (result.IsSuccess)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await action(result.Value!).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Error(ex, "Exception in OnSuccessAsync callback");
            }
        }

        return result;
    }

    #endregion

    #region Retry Operations

    /// <summary>
    ///     Retries an operation according to the specified policy
    /// </summary>
    public static async ValueTask<Result<T, TError>> RetryAsync<T, TError>(
        this Func<CancellationToken, ValueTask<Result<T, TError>>> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(policy);

        var exceptions = new List<Exception>();
        var attemptNumber = 0;

        while (attemptNumber < policy.MaxAttempts)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await operation(cancellationToken).ConfigureAwait(false);

                if (result.IsSuccess || attemptNumber == policy.MaxAttempts - 1)
                {
                    return result;
                }

                if (result.Error is not null)
                {
                    Logger.Warning("Attempt {AttemptNumber} failed: {ErrorMessage}",
                        attemptNumber + 1, result.Error.Message);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                exceptions.Add(ex);
                Logger.Warning(ex, "Attempt {AttemptNumber} threw exception", attemptNumber + 1);

                if (attemptNumber == policy.MaxAttempts - 1)
                {
                    return CreateFailureFromExceptions<T, TError>(policy, exceptions);
                }
            }

            if (attemptNumber < policy.MaxAttempts - 1)
            {
                var delay = policy.DelayStrategy(attemptNumber);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            attemptNumber++;
        }

        return CreateFailureFromExceptions<T, TError>(policy, exceptions);
    }

    /// <summary>
    ///     Retries an operation with exponential backoff
    /// </summary>
    public static ValueTask<Result<T, TError>> RetryWithExponentialBackoffAsync<T, TError>(
        this Func<CancellationToken, ValueTask<Result<T, TError>>> operation,
        int maxAttempts,
        TimeSpan initialDelay,
        Func<Exception, TError> errorMapper,
        double backoffFactor = 2.0,
        CancellationToken cancellationToken = default)
        where TError : ResultError
    {
        var policy = RetryPolicy.Create(
            maxAttempts,
            attempt => TimeSpan.FromTicks(
                (long)(initialDelay.Ticks * Math.Pow(backoffFactor, attempt))),
            errorMapper);

        return operation.RetryAsync(policy, cancellationToken);
    }

    #endregion

    #region Private Helper Methods

    private static Result<T, TError> CreateFailureFromExceptions<T, TError>(
        RetryPolicy policy,
        IReadOnlyCollection<Exception> exceptions)
        where TError : ResultError
    {
        var aggregateException = exceptions.Count == 1
            ? exceptions.First()
            : new AggregateException("Multiple exceptions occurred during retry", exceptions);

        return Result<T, TError>.Failure(
            (TError)policy.ErrorMapper(aggregateException));
    }

    #endregion
}
