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
}
