#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents errors that can occur during payload operations
/// </summary>
public sealed record PayloadError : ResultError
{
    public PayloadError(string message) : base(message) { }

    public ReadOnlyMemory<byte>? Payload { get; init; }
    public string? Hash { get; init; }
}
