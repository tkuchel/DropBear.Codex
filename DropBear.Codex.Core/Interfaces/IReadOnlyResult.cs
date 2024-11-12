#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Interfaces;

public interface IReadOnlyResult<out T, out TError> : IResult<TError>
    where TError : ResultError
{
    T? Value { get; }
}
