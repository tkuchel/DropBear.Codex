namespace DropBear.Codex.Hashing.Interfaces;

/// <summary>
///     Defines a contract for a hash builder that provides hasher instances based on a key.
/// </summary>
public interface IHashBuilder
{
    /// <summary>
    ///     Retrieves a hasher instance associated with the specified key.
    /// </summary>
    /// <param name="key">The key identifying the hasher to retrieve.</param>
    /// <returns>An instance of IHasher associated with the given key.</returns>
    IHasher GetHasher(string key);
}
