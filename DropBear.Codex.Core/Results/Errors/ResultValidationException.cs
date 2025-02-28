namespace DropBear.Codex.Core.Results.Errors;

public sealed class ResultValidationException : ResultException
{
    public ResultValidationException(string message) : base(message) { }
}
