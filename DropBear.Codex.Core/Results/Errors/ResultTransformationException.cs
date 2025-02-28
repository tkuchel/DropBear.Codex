namespace DropBear.Codex.Core.Results.Errors;

public sealed class ResultTransformationException : ResultException
{
    public ResultTransformationException(string message) : base(message) { }
    public ResultTransformationException(string message, Exception inner) : base(message, inner) { }
}
