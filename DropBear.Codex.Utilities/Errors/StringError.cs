#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for string operations.
/// </summary>
public sealed record StringError : ResultError
{
    public StringError(string message, Exception? exception = null)
        : base(message, DateTime.UtcNow)
    {
        Metadata = exception is not null
            ? new Dictionary<string, object>(StringComparer.Ordinal) { { "Exception", exception.Message } }
            : null;
    }
}
