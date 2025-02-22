namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Custom exception types for Result operations.
/// </summary>
public class ResultException : Exception
{
    public ResultException(string message) : base(message) { }
    public ResultException(string message, Exception inner) : base(message, inner) { }
}

public sealed class ResultValidationException : ResultException
{
    public ResultValidationException(string message) : base(message) { }
}

public sealed class ResultTransformationException : ResultException
{
    public ResultTransformationException(string message) : base(message) { }
    public ResultTransformationException(string message, Exception inner) : base(message, inner) { }
}
