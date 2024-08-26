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
    /// <returns>A task representing the asynchronous operation. The result is the encoded data.</returns>
    Task<byte[]> EncodeAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously decodes encoded data.
    /// </summary>
    /// <param name="encodedData">The encoded data to decode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation. The result is the decoded data.</returns>
    Task<byte[]> DecodeAsync(byte[] encodedData, CancellationToken cancellationToken = default);
}
