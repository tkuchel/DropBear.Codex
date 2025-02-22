#region

using System.Diagnostics;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Extension methods for ResultError.
/// </summary>
public static class ResultErrorExtensions
{
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
}



