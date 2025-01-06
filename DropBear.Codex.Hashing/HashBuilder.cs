#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Hashing.Hashers;
using DropBear.Codex.Hashing.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing;

/// <summary>
///     Provides a flexible way to construct and retrieve hasher instances by key.
///     Pre-registers a set of default hasher services, and allows custom registration.
/// </summary>
public sealed class HashBuilder : IHashBuilder
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, Func<IHasher>> _serviceConstructors;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HashBuilder" /> class and configures default hasher services.
    /// </summary>
    public HashBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<HashBuilder>();

        // Pre-register default hasher keys -> constructors
        _serviceConstructors = new Dictionary<string, Func<IHasher>>(StringComparer.OrdinalIgnoreCase)
        {
            { "argon2", () => new Argon2Hasher() },
            { "blake2", () => new Blake2Hasher() },
            { "blake3", () => new Blake3Hasher() },
            { "fnv1a", () => new Fnv1AHasher() },
            { "murmur3", () => new Murmur3Hasher() },
            { "siphash", () => new SipHasher(new byte[16]) }, // Example: default 16-byte key
            { "xxhash", () => new XxHasher() },
            { "extended_blake3", () => new ExtendedBlake3Hasher() } // Extended Blake3
        };

        _logger.Information("HashBuilder initialized with default hasher services.");
    }

    /// <summary>
    ///     Retrieves a hasher instance based on the specified key.
    /// </summary>
    /// <param name="key">A string key identifying the hasher (e.g. "argon2", "blake2").</param>
    /// <returns>An <see cref="IHasher" /> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if no hasher is registered for the given key.</exception>
    public IHasher GetHasher(string key)
    {
        _logger.Debug("Attempting to retrieve hasher for key: {Key}", key);

        if (!_serviceConstructors.TryGetValue(key, out var constructor))
        {
            _logger.Error("No hashing service registered for key: {Key}", key);
            throw new ArgumentException($"No hashing service registered for key: {key}", nameof(key));
        }

        _logger.Debug("Hasher for key: {Key} successfully retrieved.", key);
        return constructor();
    }

    /// <summary>
    ///     Registers a custom hasher for a given key, overriding any existing registration.
    /// </summary>
    /// <param name="key">A string key identifying the hasher.</param>
    /// <param name="hasherFactory">
    ///     A factory function returning a new <see cref="IHasher" /> instance whenever called.
    /// </param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key" /> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="hasherFactory" /> is null.</exception>
    public void RegisterHasher(string key, Func<IHasher> hasherFactory)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Error("Attempted to register a hasher with an empty or null key.");
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        _serviceConstructors[key] = hasherFactory ?? throw new ArgumentNullException(nameof(hasherFactory));

        _logger.Debug("Hasher registered for key: {Key}", key);
    }
}
