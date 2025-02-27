#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;

#endregion

namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for compressors, defining methods to compress and decompress data.
/// </summary>
public interface ICompressor
{
    /// <summary>
    ///     Asynchronously compresses data.
    /// </summary>
    /// <param name="data">The data to compress.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A result containing either the compressed data or a compression error.</returns>
    Task<Result<byte[], SerializationError>> CompressAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously decompresses compressed data.
    /// </summary>
    /// <param name="compressedData">The compressed data to decompress.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A result containing either the decompressed data or a compression error.</returns>
    Task<Result<byte[], SerializationError>> DecompressAsync(byte[] compressedData,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets information about the compression algorithm and its capabilities.
    /// </summary>
    /// <returns>A dictionary containing compression information.</returns>
    IDictionary<string, object> GetCompressionInfo();
}
