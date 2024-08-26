namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for compression providers.
/// </summary>
public interface ICompressionProvider
{
    /// <summary>
    ///     Get an instance of the compressor.
    /// </summary>
    /// <returns>An instance of the compressor.</returns>
    ICompressor GetCompressor();
}
