#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Base interface for all result types in the library, providing a common
///     set of properties to indicate success/failure state and associated exceptions.
/// </summary>
public interface IResult
{
    /// <summary>
    ///     Gets a value indicating whether the result is in a success state.
    ///     (Typically determined by <see cref="ResultState" />.)
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    ///     Gets the <see cref="ResultState" /> indicating overall success/failure or other states.
    /// </summary>
    ResultState State { get; }

    /// <summary>
    ///     If a failure has occurred, this is the main exception, if any.
    ///     (Maybe <see langword="null"/> if the error did not arise from a thrown exception.)
    /// </summary>
    Exception? Exception { get; }

    /// <summary>
    ///     Gets any additional exceptions aggregated by the operation.
    ///     Usually this is either zero or one exception, but in
    ///     some operations multiple exceptions may be collected.
    /// </summary>
    IReadOnlyCollection<Exception> Exceptions { get; }
}

/// <summary>
///     Interface representing a result that can hold a specific error type <typeparamref name="TError" />,
///     extending the basic <see cref="IResult" /> interface.
/// </summary>
/// <typeparam name="TError">A type that inherits from <see cref="ResultError" />.</typeparam>
public interface IResult<out TError> : IResult
    where TError : ResultError
{
    /// <summary>
    ///     Gets the error object (if any) that describes what went wrong
    ///     when <see cref="IResult.IsSuccess" /> is false.
    ///     This property is covariant, meaning it can be cast to a more derived type.
    /// </summary>
    TError? Error { get; }
}
