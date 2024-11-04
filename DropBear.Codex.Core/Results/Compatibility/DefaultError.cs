#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     Backwards compatible Result type using default error type
/// </summary>
public record DefaultError : ResultError
{
    public DefaultError(string message) : base(message) { }
}
