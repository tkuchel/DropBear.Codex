#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Interfaces;

/// <summary>
///     A read-only interface representing a result that has a value of type <typeparamref name="T" />
///     and an error of type <typeparamref name="TError" /> (derived from <see cref="ResultError" />).
///     This interface is typically used for results where the value can be inspected but not modified.
/// </summary>
/// <typeparam name="T">The type of the value that the result holds (if successful).</typeparam>
/// <typeparam name="TError">A type deriving from <see cref="ResultError" /> representing the error.</typeparam>
public interface IReadOnlyResult<out T, out TError> : IResult<TError>
    where TError : ResultError
{
    /// <summary>
    ///     Gets the value of the result, or <c>null</c> if the result is not successful or
    ///     if the value itself is null.
    /// </summary>
    T? Value { get; }
}
