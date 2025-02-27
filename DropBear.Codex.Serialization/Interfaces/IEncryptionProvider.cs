namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for encryption providers.
/// </summary>
public interface IEncryptionProvider
{
    /// <summary>
    ///     Gets an encryptor.
    /// </summary>
    /// <returns>An instance of an encryptor.</returns>
    IEncryptor GetEncryptor();

    /// <summary>
    ///     Gets information about the encryption provider.
    /// </summary>
    /// <returns>A dictionary of information about the encryption provider.</returns>
    IDictionary<string, object> GetProviderInfo();
}
