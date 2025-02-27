namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for compression providers.
/// </summary>
public interface ICompressionProvider
{
    /// <summary>
    ///     Gets an instance of the compressor.
    /// </summary>
    /// <returns>An instance of the compressor.</returns>
    ICompressor GetCompressor();

    /// <summary>
    ///     Gets information about the compression provider.
    /// </summary>
    /// <returns>A dictionary of information about the compression provider.</returns>
    IDictionary<string, object> GetProviderInfo();
}
