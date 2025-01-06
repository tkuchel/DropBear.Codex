#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Interface representing a result that can hold a specific error type <typeparamref name="TError" />,
///     extending the basic <see cref="IResult" /> interface.
/// </summary>
/// <typeparam name="TError">A type that inherits from <see cref="ResultError" />.</typeparam>
public interface IResult<out TError> : IResult
    where TError : ResultError
{
    /// <summary>
    ///     Gets the error object (if any) that describes what went wrong when <see cref="IResult.IsSuccess" /> is false.
    /// </summary>
    TError? Error { get; }
}
