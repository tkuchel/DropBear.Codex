#region

using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     Base interface for all result types in the library, providing a common
///     set of properties to indicate success/failure state and associated exceptions.
/// </summary>
public interface IResult
{
    /// <summary>
    ///     Gets the <see cref="ResultState" /> indicating the success or failure status of this result.
    /// </summary>
    ResultState State { get; }

    /// <summary>
    ///     Gets a value indicating whether this result represents a successful operation.
    /// </summary>
    bool IsSuccess { get; }

    /// <summary>
    ///     Gets an optional exception if the result represents a failure that threw an exception.
    /// </summary>
    Exception? Exception { get; }

    /// <summary>
    ///     Gets a read-only collection of exceptions if multiple exceptions occurred.
    ///     Typically, this might only contain one exception, but some operations may aggregate exceptions.
    /// </summary>
    IReadOnlyCollection<Exception> Exceptions { get; }
}
