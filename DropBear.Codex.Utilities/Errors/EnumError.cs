#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Utilities.Errors;

/// <summary>
///     Custom error class for enum operations.
/// </summary>
public sealed record EnumError : ResultError
{
    public EnumError(string message, Exception? exception = null)
        : base(message, DateTime.UtcNow)
    {
        Metadata = exception is not null
            ? new Dictionary<string, object>(StringComparer.Ordinal) { { "Exception", exception.Message } }
            : null;
    }
}
