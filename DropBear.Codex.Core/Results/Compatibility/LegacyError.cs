#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     Legacy error type for backwards compatibility.
/// </summary>
public sealed record LegacyError : ResultError
{
    public LegacyError(string message) : base(message) { }
}
