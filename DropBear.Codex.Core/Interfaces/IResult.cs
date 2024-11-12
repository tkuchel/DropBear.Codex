#region

using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Interfaces;

public interface IResult
{
    ResultState State { get; }
    bool IsSuccess { get; }
    Exception? Exception { get; }
    IReadOnlyCollection<Exception> Exceptions { get; }
}
