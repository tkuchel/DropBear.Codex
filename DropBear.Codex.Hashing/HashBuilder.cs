#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Hashing.Hashers;
using DropBear.Codex.Hashing.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing;

/// <summary>
///     Provides a flexible way to construct and retrieve hasher instances by key.
/// </summary>
public class HashBuilder : IHashBuilder
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, Func<IHasher>> _serviceConstructors;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HashBuilder" /> class and configures default hasher services.
    /// </summary>
    public HashBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<HashBuilder>();
        _serviceConstructors = new Dictionary<string, Func<IHasher>>(StringComparer.OrdinalIgnoreCase)
        {
            { "argon2", () => new Argon2Hasher() },
            { "blake2", () => new Blake2Hasher() },
            { "blake3", () => new Blake3Hasher() },
            { "fnv1a", () => new Fnv1AHasher() },
            { "murmur3", () => new Murmur3Hasher() },
            { "siphash", () => new SipHasher(new byte[16]) }, // Assumes the key is predefined and static
            { "xxhash", () => new XxHasher() },
            { "extended_blake3", () => new ExtendedBlake3Hasher() } // Extended Blake3 Service
        };

        _logger.Information("HashBuilder initialized with default hasher services.");
    }

    /// <summary>
    ///     Retrieves a hasher instance based on the specified key.
    /// </summary>
    /// <param name="key">The key identifying the hasher service.</param>
    /// <returns>The corresponding hasher instance.</returns>
    /// <exception cref="ArgumentException">Thrown if no hasher is registered with the provided key.</exception>
    public IHasher GetHasher(string key)
    {
        _logger.Debug("Attempting to retrieve hasher for key: {Key}", key);

        if (!_serviceConstructors.TryGetValue(key, out var constructor))
        {
            _logger.Error("No hashing service registered for key: {Key}", key);
            throw new ArgumentException($"No hashing service registered for key: {key}", nameof(key));
        }

        _logger.Information("Hasher for key: {Key} successfully retrieved.", key);
        return constructor();
    }

    /// <summary>
    ///     Registers a custom hasher with the specified key.
    /// </summary>
    /// <param name="key">The key to identify the hasher.</param>
    /// <param name="hasherFactory">The factory function to create the hasher instance.</param>
    public void RegisterHasher(string key, Func<IHasher> hasherFactory)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Error("Attempted to register a hasher with an empty or null key.");
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        _serviceConstructors[key] = hasherFactory ?? throw new ArgumentNullException(nameof(hasherFactory));
        _logger.Information("Hasher registered for key: {Key}", key);
    }
}
