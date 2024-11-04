namespace DropBear.Codex.Core.Results.Base;

/// <summary>
///     Base class for error types
/// </summary>
public abstract record ResultError
{
    protected ResultError(string message)
    {
        Message = message;
    }

    public string Message { get; init; }
}
