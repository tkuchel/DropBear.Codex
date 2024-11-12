#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Interfaces;

public interface IResult<T, TError> : IResult<TError>
    where TError : ResultError
{
    T? Value { get; }
    IResult<T, TError> Ensure(Func<T, bool> predicate, TError error);
    T ValueOrDefault(T defaultValue = default!);
    T ValueOrThrow(string? errorMessage = null);
}
