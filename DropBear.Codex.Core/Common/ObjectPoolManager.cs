#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Interfaces;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Common;

/// <summary>
///     Provides a simplified object pooling infrastructure for DropBear.Codex.
///     Optimized for .NET 9 with focus on the compatibility layer only.
///     Uses Microsoft.Extensions.ObjectPool for standard patterns.
/// </summary>
public static class ObjectPoolManager
{
    /// <summary>
    ///     The maximum number of retained objects in each pool by default.
    /// </summary>
    private const int DefaultPoolSize = 1024;

    /// <summary>
    ///     The global DefaultObjectPoolProvider used to create individual pools.
    /// </summary>
    private static readonly DefaultObjectPoolProvider Provider = new() { MaximumRetained = DefaultPoolSize };

    /// <summary>
    ///     A cache of ObjectPool instances keyed by type name.
    ///     Uses ordinal string comparison for better performance.
    /// </summary>
    private static readonly ConcurrentDictionary<string, object> TypedPools =
        new(StringComparer.Ordinal);

    #region Public Pool Accessors

    /// <summary>
    ///     Creates or retrieves a shared ObjectPool for the specified type.
    ///     Primarily used by the legacy compatibility layer.
    /// </summary>
    /// <typeparam name="T">The type of objects to pool. Must have a parameterless constructor.</typeparam>
    /// <param name="factory">
    ///     An optional delegate used to create new instances of T.
    ///     If not provided, T must have a public parameterless constructor.
    /// </param>
    /// <returns>An ObjectPool instance associated with type T.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ObjectPool<T> GetPool<T>(Func<T>? factory = null)
        where T : class
    {
        var typeName = typeof(T).FullName ?? typeof(T).Name;

        // Fast path: return existing pool if available
        if (TypedPools.TryGetValue(typeName, out var existingPool) &&
            existingPool is ObjectPool<T> typedPool)
        {
            return typedPool;
        }

        // Slow path: create new pool
        return CreateAndCachePool(typeName, factory);
    }

    /// <summary>
    ///     Retrieves one object of type T from the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Get<T>() where T : class
    {
        var pool = GetPool<T>();
        return pool.Get();
    }

    /// <summary>
    ///     Returns an instance of T to its pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return<T>(T? obj) where T : class
    {
        if (obj is not null)
        {
            var pool = GetPool<T>();
            pool.Return(obj);
        }
    }

    #endregion

    #region Pool Creation

    /// <summary>
    ///     Creates and caches a new pool for the specified type.
    /// </summary>
    private static ObjectPool<T> CreateAndCachePool<T>(string typeName, Func<T>? factory)
        where T : class
    {
        ObjectPool<T> newPool;

        if (factory is null)
        {
            // Ensure type has parameterless constructor
            if (!HasParameterlessConstructor(typeof(T)))
            {
                throw new InvalidOperationException(
                    $"Cannot create an object pool for type {typeof(T).Name} " +
                    "because it lacks a public parameterless constructor. " +
                    "Provide a factory delegate instead.");
            }

            var policy = new DefaultPoolPolicy<T>();
            newPool = Provider.Create(policy);
        }
        else
        {
            var policy = new DelegatePoolPolicy<T>(factory);
            newPool = Provider.Create(policy);
        }

        // Cache the pool
        TypedPools.TryAdd(typeName, newPool);
        return newPool;
    }

    /// <summary>
    ///     Checks whether the given type has a public parameterless constructor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasParameterlessConstructor(Type type)
    {
        return type.GetConstructor(Type.EmptyTypes) is not null;
    }

    #endregion

    #region Cleanup

    /// <summary>
    ///     Clears all pools. Use with caution.
    ///     Primarily for testing purposes.
    /// </summary>
    public static void ClearAllPools()
    {
        TypedPools.Clear();
    }

    #endregion
}

#region Pool Policies

/// <summary>
///     Default pool policy that creates objects via reflection.
///     Optimized for .NET 9 with cached constructor info.
/// </summary>
file sealed class DefaultPoolPolicy<T> : PooledObjectPolicy<T>
    where T : class
{
    private readonly Func<T> _factory;

    public DefaultPoolPolicy()
    {
        var ctor = typeof(T).GetConstructor(Type.EmptyTypes);
        if (ctor is null)
        {
            throw new InvalidOperationException(
                $"Type {typeof(T).Name} does not have a public parameterless constructor.");
        }

        // Cache compiled constructor for better performance
        _factory = () => (T)ctor.Invoke(null)!;
    }

    public override T Create() => _factory();

    public override bool Return(T obj)
    {
        // Support IPooledResult interface for objects that need cleanup
        if (obj is IPooledResult pooled)
        {
            try
            {
                pooled.Reset();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting pooled object: {ex.Message}");
                return false;
            }
        }

        return true;
    }
}

/// <summary>
///     Pool policy that uses a user-supplied delegate to create new objects.
/// </summary>
file sealed class DelegatePoolPolicy<T> : PooledObjectPolicy<T>
    where T : class
{
    private readonly Func<T> _factory;

    public DelegatePoolPolicy(Func<T> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public override T Create() => _factory();

    public override bool Return(T obj)
    {
        if (obj is IPooledResult pooled)
        {
            try
            {
                pooled.Reset();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting pooled object: {ex.Message}");
                return false;
            }
        }

        return true;
    }
}

#endregion
