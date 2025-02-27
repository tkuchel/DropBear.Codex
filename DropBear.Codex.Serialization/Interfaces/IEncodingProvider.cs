namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for encoding providers.
/// </summary>
public interface IEncodingProvider
{
    /// <summary>
    ///     Gets an encoder.
    /// </summary>
    /// <returns>An instance of an encoder.</returns>
    IEncoder GetEncoder();

    /// <summary>
    ///     Gets information about the encoding provider.
    /// </summary>
    /// <returns>A dictionary of information about the encoding provider.</returns>
    IDictionary<string, object> GetProviderInfo();
}
