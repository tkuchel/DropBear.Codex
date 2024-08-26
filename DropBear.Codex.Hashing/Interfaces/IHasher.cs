#region

using DropBear.Codex.Core;

#endregion

namespace DropBear.Codex.Hashing.Interfaces;

/// <summary>
///     Defines a contract for hashers within the system, supporting basic hashing and configuration via a fluent API.
/// </summary>
public interface IHasher
{
    /// <summary>
    ///     Computes a hash for the given input.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A Result containing the hash as a string or an error message.</returns>
    Result<string> Hash(string input);

    /// <summary>
    ///     Verifies if the given input matches the expected hash.
    /// </summary>
    /// <param name="input">The input string to verify.</param>
    /// <param name="expectedHash">The expected hash value to compare against.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Result Verify(string input, string expectedHash);

    /// <summary>
    ///     Encodes the given byte array to a Base64 hash string.
    /// </summary>
    /// <param name="data">The byte array to hash.</param>
    /// <returns>A Result containing the Base64 encoded hash or an error message.</returns>
    Result<string> EncodeToBase64Hash(byte[] data);

    /// <summary>
    ///     Verifies a Base64 encoded hash against the given byte array.
    /// </summary>
    /// <param name="data">The byte array to verify.</param>
    /// <param name="expectedBase64Hash">The expected Base64 encoded hash to compare against.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Result VerifyBase64Hash(byte[] data, string expectedBase64Hash);

    /// <summary>
    ///     Configures the hasher with a salt.
    /// </summary>
    /// <param name="salt">The salt bytes to use in hashing operations.</param>
    /// <returns>The configured hasher instance.</returns>
    IHasher WithSalt(byte[]? salt);

    /// <summary>
    ///     Configures the hasher with a specific number of iterations.
    /// </summary>
    /// <param name="iterations">The number of iterations to use in hashing operations.</param>
    /// <returns>The configured hasher instance.</returns>
    IHasher WithIterations(int iterations);
}
