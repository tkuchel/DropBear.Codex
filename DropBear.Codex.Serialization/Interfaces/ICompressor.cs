namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for compressors.
/// </summary>
public interface ICompressor
{
    /// <summary>
    ///     Asynchronously compresses data.
    /// </summary>
    /// <param name="data">The data to compress.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation. The result is the compressed data.</returns>
    Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously decompresses compressed data.
    /// </summary>
    /// <param name="compressedData">The compressed data to decompress.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation. The result is the decompressed data.</returns>
    Task<byte[]> DecompressAsync(byte[] compressedData, CancellationToken cancellationToken = default);
}
