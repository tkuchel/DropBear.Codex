namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for encryptors.
/// </summary>
public interface IEncryptor
{
    /// <summary>
    ///     Asynchronously encrypts the provided data.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the encrypted data.</returns>
    Task<byte[]> EncryptAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously decrypts the provided encrypted data.
    /// </summary>
    /// <param name="data">The encrypted data to decrypt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation. The result contains the decrypted data.</returns>
    Task<byte[]> DecryptAsync(byte[] data, CancellationToken cancellationToken = default);
}
