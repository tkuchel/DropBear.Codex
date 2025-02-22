#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Default implementation of result error handling.
/// </summary>
public sealed class DefaultResultErrorHandler : IResultErrorHandler
{
    private readonly IResultTelemetry _telemetry;

    public DefaultResultErrorHandler(IResultTelemetry telemetry)
    {
        _telemetry = telemetry;
    }

    public Result<T, TError> HandleError<T, TError>(Exception exception)
        where TError : ResultError
    {
        _telemetry.TrackException(exception, ResultState.Failure, typeof(Result<T, TError>));
        var error = CreateError<TError>(exception);
        return Result<T, TError>.Failure(error, exception);
    }

    public Result<T, TError> HandleError<T, TError>(TError error)
        where TError : ResultError
    {
        return Result<T, TError>.Failure(error);
    }

    private static TError CreateError<TError>(Exception exception)
        where TError : ResultError
    {
        return (TError)Activator.CreateInstance(typeof(TError), exception.Message)!;
    }
}
