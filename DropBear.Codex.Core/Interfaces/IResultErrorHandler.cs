#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Provides custom error handling capabilities for results.
/// </summary>
public interface IResultErrorHandler
{
    Result<T, TError> HandleError<T, TError>(Exception exception)
        where TError : ResultError;

    Result<T, TError> HandleError<T, TError>(TError error)
        where TError : ResultError;
}
