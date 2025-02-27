#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;

#endregion

namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for encoders.
/// </summary>
public interface IEncoder
{
    /// <summary>
    ///     Asynchronously encodes data.
    /// </summary>
    /// <param name="data">The data to encode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing either the encoded data or an error.</returns>
    Task<Result<byte[], SerializationError>> EncodeAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously decodes encoded data.
    /// </summary>
    /// <param name="encodedData">The encoded data to decode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result containing either the decoded data or an error.</returns>
    Task<Result<byte[], SerializationError>> DecodeAsync(byte[] encodedData,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets information about the encoder.
    /// </summary>
    /// <returns>A dictionary containing encoder information.</returns>
    IDictionary<string, object> GetEncoderInfo();
}
