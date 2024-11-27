#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Blazor.Exceptions;

public record SnackbarError : ResultError
{
    public SnackbarError(string message) : base(message) { }
}
