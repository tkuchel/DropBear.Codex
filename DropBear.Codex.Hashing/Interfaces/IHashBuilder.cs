#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;
using DropBear.Codex.Hashing.Interfaces;

#endregion

namespace DropBear.Codex.Hashing;

/// <summary>
///     Defines a contract for building and retrieving IHasher instances.
/// </summary>
public interface IHashBuilder
{
    /// <summary>
    ///     Retrieves a hasher instance based on the specified key.
    /// </summary>
    /// <param name="key">A string key identifying the hasher (e.g. "argon2", "blake2").</param>
    /// <returns>An <see cref="IHasher" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if no hasher is registered for the given key.</exception>
    IHasher GetHasher(string key);

    /// <summary>
    ///     Attempts to retrieve a hasher instance based on the specified key.
    /// </summary>
    /// <param name="key">A string key identifying the hasher (e.g. "argon2", "blake2").</param>
    /// <returns>A Result containing the hasher or an error if not found.</returns>
    Result<IHasher, BuilderError> TryGetHasher(string key);

    /// <summary>
    ///     Registers a custom hasher for a given key, overriding any existing registration.
    /// </summary>
    /// <param name="key">A string key identifying the hasher.</param>
    /// <param name="hasherFactory">
    ///     A factory function returning a new <see cref="IHasher" /> instance whenever called.
    /// </param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="hasherFactory" /> is null.</exception>
    void RegisterHasher(string key, Func<IHasher> hasherFactory);

    /// <summary>
    ///     Enables object pooling for a specific hasher type to improve performance.
    /// </summary>
    /// <param name="key">The hasher key to enable pooling for.</param>
    /// <param name="maxPoolSize">Maximum number of objects to pool (default 32).</param>
    /// <exception cref="ArgumentException">Thrown if the key is invalid or not found.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if pool size is less than 1.</exception>
    void EnablePoolingForHasher(string key, int maxPoolSize = 32);
}
