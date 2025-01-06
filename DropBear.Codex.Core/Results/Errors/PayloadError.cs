#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Errors;

/// <summary>
///     Represents errors that occur while handling some form of payload (e.g., file or data stream).
///     May include an optional <see cref="Payload" /> and a <see cref="Hash" />.
/// </summary>
public sealed record PayloadError : ResultError
{
    /// <summary>
    ///     Creates a new <see cref="PayloadError" /> with the specified message.
    /// </summary>
    /// <param name="message">A descriptive error message.</param>
    public PayloadError(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Gets an optional payload as a <see cref="ReadOnlyMemory{T}" /> (e.g., raw bytes) associated with the error.
    /// </summary>
    public ReadOnlyMemory<byte>? Payload { get; init; }

    /// <summary>
    ///     Gets an optional string representing a hash of the payload or some unique identifier.
    /// </summary>
    public string? Hash { get; init; }
}
