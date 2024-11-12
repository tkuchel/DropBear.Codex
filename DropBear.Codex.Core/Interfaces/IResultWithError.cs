#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Interfaces;

public interface IResult<out TError> : IResult where TError : ResultError
{
    TError? Error { get; }
}
