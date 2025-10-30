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
/// <remarks>
///     This implementation includes object pooling for frequently used hashers to improve performance
///     and reduce GC pressure in high-throughput scenarios.
/// </remarks>
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
            // Enhanced SipHasher with random key generation
            { "siphash", () => new SipHasher() },
            { "xxhash", () => new XxHasher() },
            { "extended_blake3", () => new ExtendedBlake3Hasher() }
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        _logger.Information("HashBuilder initialized with default hasher services.");
    }

    /// <inheritdoc />
    public IHasher GetHasher(string key)
    {
        _logger.Debug("Attempting to retrieve hasher for key: {Key}", key);

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Error("Hasher key cannot be null or empty.");
            throw new ArgumentException("Hasher key cannot be null or empty.", nameof(key));
        }

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

    /// <inheritdoc />
    public Result<IHasher, BuilderError> TryGetHasher(string key)
    {
        _logger.Debug("Attempting to retrieve hasher for key: {Key}", key);

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.Error("Hasher key cannot be null or empty.");
            return Result<IHasher, BuilderError>.Failure(
                new BuilderError("Hasher key cannot be null or empty."));
        }

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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
    ///     Returns a hasher to the pool when it's no longer needed.
    ///     This can improve performance in high-throughput scenarios.
    /// </summary>
    /// <param name="key">The key used to retrieve the hasher.</param>
    /// <param name="hasher">The hasher instance to return to the pool.</param>
    public void ReturnHasher(string key, IHasher hasher)
    {
        if (string.IsNullOrWhiteSpace(key) || hasher == null)
        {
            return;
        }

        if (_hasherPools.TryGetValue(key, out var pool))
        {
            pool.Return(hasher);
            _logger.Debug("Returned hasher for key: {Key} to pool", key);
        }
    }

    /// <summary>
    ///     Tries to get a hasher from the pool if available, otherwise creates a new one.
    /// </summary>
    /// <param name="key">The hasher key.</param>
    /// <returns>A result containing the hasher or an error.</returns>
    public Result<PooledHasher, BuilderError> GetPooledHasher(string key)
    {
        var hasherResult = TryGetHasher(key);
        if (!hasherResult.IsSuccess)
        {
            return Result<PooledHasher, BuilderError>.Failure(hasherResult.Error!);
        }

        // Create a wrapper that will automatically return the hasher to the pool
        var pooledHasher = new PooledHasher(hasherResult.Value, key, this);
        return Result<PooledHasher, BuilderError>.Success(pooledHasher);
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

/// <summary>
///     A wrapper that automatically returns a hasher to the pool when disposed.
/// </summary>
public sealed class PooledHasher : IDisposable
{
    private readonly HashBuilder _builder;
    private readonly string _key;
    private bool _disposed;

    internal PooledHasher(IHasher hasher, string key, HashBuilder builder)
    {
        Hasher = hasher;
        _key = key;
        _builder = builder;
    }

    /// <summary>
    ///     Gets the underlying hasher.
    /// </summary>
    public IHasher Hasher { get; }

    /// <summary>
    ///     Disposes the wrapper and returns the hasher to the pool.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _builder.ReturnHasher(_key, Hasher);
            _disposed = true;
        }
    }
}
