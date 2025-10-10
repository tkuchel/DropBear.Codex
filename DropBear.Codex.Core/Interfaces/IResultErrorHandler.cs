#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Provides custom error handling capabilities for results.
/// </summary>
public interface IResultErrorHandler
{
    /// <summary>
    ///     Handles exceptions by converting them to result failures.
    /// </summary>
    Result<T, TError> HandleError<T, TError>(Exception exception)
        where TError : ResultError;

    /// <summary>
    ///     Handles errors by creating appropriate result failures.
    /// </summary>
    Result<T, TError> HandleError<T, TError>(TError error)
        where TError : ResultError;
}
