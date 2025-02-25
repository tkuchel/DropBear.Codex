#region

using System.Collections.Concurrent;
using System.Collections.Frozen;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Hashing.Errors;
using DropBear.Codex.Hashing.Hashers;
using DropBear.Codex.Hashing.Interfaces;
using Microsoft.Extensions.ObjectPool;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing;

/// <summary>
///     Provides a flexible way to construct and retrieve hasher instances by key.
///     Pre-registers a set of default hasher services, and allows custom registration.
/// </summary>
public sealed class HashBuilder : IHashBuilder
{
    private readonly Dictionary<string, Func<IHasher>> _customHashers;
    private readonly FrozenDictionary<string, Func<IHasher>> _defaultHashers;

    // Object pools for frequently used hashers
    private readonly ConcurrentDictionary<string, ObjectPool<IHasher>> _hasherPools =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HashBuilder" /> class and configures default hasher services.
    /// </summary>
    public HashBuilder()
    {
        _logger = LoggerFactory.Logger.ForContext<HashBuilder>();
        _customHashers = new Dictionary<string, Func<IHasher>>(StringComparer.OrdinalIgnoreCase);

        // Pre-register default hasher keys -> constructors with frozen dictionary for better performance
        _defaultHashers = new Dictionary<string, Func<IHasher>>(StringComparer.OrdinalIgnoreCase)
        {
            { "argon2", () => new Argon2Hasher() },
            { "blake2", () => new Blake2Hasher() },
            { "blake3", () => new Blake3Hasher() },
            { "fnv1a", () => new Fnv1AHasher() },
            { "murmur3", () => new Murmur3Hasher() },
            { "siphash", () => new SipHasher(new byte[16]) }, // Example: default 16-byte key
            { "xxhash", () => new XxHasher() },
            { "extended_blake3", () => new ExtendedBlake3Hasher() } // Extended Blake3
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

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

        // Check for pooled hasher first
        if (_hasherPools.TryGetValue(key, out var pool))
        {
            var hasher = pool.Get();
            _logger.Debug("Retrieved hasher for key: {Key} from pool", key);
            return hasher;
        }

        // Check custom hashers
        if (_customHashers.TryGetValue(key, out var customConstructor))
        {
            _logger.Debug("Retrieved custom hasher for key: {Key}", key);
            return customConstructor();
        }

        // Check default hashers
        if (_defaultHashers.TryGetValue(key, out var defaultConstructor))
        {
            _logger.Debug("Retrieved default hasher for key: {Key}", key);
            return defaultConstructor();
        }

        _logger.Error("No hashing service registered for key: {Key}", key);
        throw new ArgumentException($"No hashing service registered for key: {key}", nameof(key));
    }

    /// <summary>
    ///     Attempts to retrieve a hasher instance based on the specified key.
    /// </summary>
    /// <param name="key">A string key identifying the hasher (e.g. "argon2", "blake2").</param>
    /// <returns>A Result containing the hasher or an error if not found.</returns>
    public Result<IHasher, BuilderError> TryGetHasher(string key)
    {
        _logger.Debug("Attempting to retrieve hasher for key: {Key}", key);

        try
        {
            // Check for pooled hasher first
            if (_hasherPools.TryGetValue(key, out var pool))
            {
                var hasher = pool.Get();
                _logger.Debug("Retrieved hasher for key: {Key} from pool", key);
                return Result<IHasher, BuilderError>.Success(hasher);
            }

            // Check custom hashers
            if (_customHashers.TryGetValue(key, out var customConstructor))
            {
                _logger.Debug("Retrieved custom hasher for key: {Key}", key);
                return Result<IHasher, BuilderError>.Success(customConstructor());
            }

            // Check default hashers
            if (_defaultHashers.TryGetValue(key, out var defaultConstructor))
            {
                _logger.Debug("Retrieved default hasher for key: {Key}", key);
                return Result<IHasher, BuilderError>.Success(defaultConstructor());
            }

            _logger.Error("No hashing service registered for key: {Key}", key);
            return Result<IHasher, BuilderError>.Failure(BuilderError.HasherNotFound(key));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving hasher for key: {Key}", key);
            return Result<IHasher, BuilderError>.Failure(
                new BuilderError($"Error retrieving hasher: {ex.Message}"), ex);
        }
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

        if (hasherFactory == null)
        {
            throw new ArgumentNullException(nameof(hasherFactory));
        }

        _customHashers[key] = hasherFactory;
        _logger.Debug("Hasher registered for key: {Key}", key);
    }

    /// <summary>
    ///     Enables object pooling for a specific hasher type to improve performance.
    /// </summary>
    /// <param name="key">The hasher key to enable pooling for.</param>
    /// <param name="maxPoolSize">Maximum number of objects to pool (default 32).</param>
    public void EnablePoolingForHasher(string key, int maxPoolSize = 32)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }

        if (maxPoolSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPoolSize), "Pool size must be positive.");
        }

        // Check if we have a factory for this key
        Func<IHasher> factory;
        if (_customHashers.TryGetValue(key, out var customFactory))
        {
            factory = customFactory;
        }
        else if (_defaultHashers.TryGetValue(key, out var defaultFactory))
        {
            factory = defaultFactory;
        }
        else
        {
            throw new ArgumentException($"No hasher registered for key: {key}", nameof(key));
        }

        // Create pool policy
        var policy = new HasherPooledObjectPolicy(factory);
        var pool = new DefaultObjectPool<IHasher>(policy, maxPoolSize);

        // Add to pooled hashers
        _hasherPools[key] = pool;
        _logger.Information("Enabled pooling for hasher type: {Key} with max size {PoolSize}", key, maxPoolSize);
    }

    /// <summary>
    ///     Policy for pooling hasher objects.
    /// </summary>
    private sealed class HasherPooledObjectPolicy : IPooledObjectPolicy<IHasher>
    {
        private readonly Func<IHasher> _factory;

        public HasherPooledObjectPolicy(Func<IHasher> factory)
        {
            _factory = factory;
        }

        public IHasher Create()
        {
            return _factory();
        }

        public bool Return(IHasher obj)
        {
            return true;
        }
    }
}
