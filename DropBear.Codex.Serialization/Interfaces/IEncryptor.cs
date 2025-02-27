#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;

#endregion

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
    /// <returns>A result containing either the encrypted data or an error.</returns>
    Task<Result<byte[], SerializationError>> EncryptAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously decrypts the provided encrypted data.
    /// </summary>
    /// <param name="data">The encrypted data to decrypt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing either the decrypted data or an error.</returns>
    Task<Result<byte[], SerializationError>> DecryptAsync(byte[] data, CancellationToken cancellationToken = default);
}
