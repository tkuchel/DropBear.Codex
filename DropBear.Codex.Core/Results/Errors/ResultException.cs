namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Custom exception types for Result operations.
/// </summary>
public class ResultException : Exception
{
    public ResultException(string message) : base(message) { }
    public ResultException(string message, Exception inner) : base(message, inner) { }
}
