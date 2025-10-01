#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Common;

/// <summary>
///     Provides a unified object pooling infrastructure for DropBear.Codex.
///     Enhanced for .NET 9 with diagnostics and monitoring capabilities.
/// </summary>
public static class ObjectPoolManager
{
    /// <summary>
    ///     The maximum number of retained objects in each pool by default.
    /// </summary>
    private const int DefaultPoolSize = 1024;

    /// <summary>
    ///     The default initial capacity used for lists, dictionaries, etc.
    /// </summary>
    private const int DefaultObjectCapacity = 32;

    /// <summary>
    ///     The global DefaultObjectPoolProvider used to create individual pools.
    /// </summary>
    private static readonly DefaultObjectPoolProvider Provider = new() { MaximumRetained = DefaultPoolSize };

    /// <summary>
    ///     A cache of ObjectPool instances keyed by type name.
    /// </summary>
    private static readonly ConcurrentDictionary<string, object> TypedPools = new(StringComparer.Ordinal);

    /// <summary>
    ///     Pool statistics tracking (optional, can be disabled).
    /// </summary>
    private static readonly ConcurrentDictionary<string, PoolStatistics> PoolStats = new(StringComparer.Ordinal);

    /// <summary>
    ///     Whether to collect pool statistics. Disabled by default for performance.
    /// </summary>
    public static bool CollectStatistics { get; set; } = false;

    #region IResettable Interface

    /// <summary>
    ///     Defines an interface for objects that can be reset to an initial state
    ///     when returned to the object pool.
    /// </summary>
    public interface IResettable
    {
        /// <summary>
        ///     Resets this object to its initial, default state before re-entering the pool.
        /// </summary>
        void Reset();
    }

    #endregion

    #region Public Pool Accessors

    /// <summary>
    ///     Creates or retrieves a shared ObjectPool for the specified type.
    /// </summary>
    /// <typeparam name="T">The type of objects to pool.</typeparam>
    /// <param name="factory">
    ///     An optional delegate used to create new instances of T.
    /// </param>
    /// <returns>An ObjectPool instance associated with type T.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ObjectPool<T> GetPool<T>(Func<T>? factory = null)
        where T : class
    {
        var typeName = typeof(T).FullName ?? typeof(T).Name;

        if (TypedPools.TryGetValue(typeName, out var existingPool) &&
            existingPool is ObjectPool<T> typedPool)
        {
            return typedPool;
        }

        ObjectPool<T> newPool;

        if (factory == null)
        {
            if (HasParameterlessConstructor(typeof(T)))
            {
                var policy = new DefaultObjectPoolPolicy<T>();
                newPool = Provider.Create(policy);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot create an object pool for type {typeof(T).Name} " +
                    "because it lacks a public parameterless constructor. Provide a factory delegate instead.");
            }
        }
        else
        {
            var policy = new DelegateObjectPoolPolicy<T>(factory);
            newPool = Provider.Create(policy);
        }

        // Wrap with statistics tracking if enabled
        if (CollectStatistics)
        {
            newPool = new StatisticsTrackingPool<T>(newPool, typeName);
            PoolStats.TryAdd(typeName, new PoolStatistics(typeName));
        }

        TypedPools.TryAdd(typeName, newPool);
        return newPool;
    }

    /// <summary>
    ///     Retrieves one object of type T from the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Get<T>()
        where T : class
    {
        var pool = GetPool<T>();
        var item = pool.Get();

        if (CollectStatistics)
        {
            var typeName = typeof(T).FullName ?? typeof(T).Name;
            if (PoolStats.TryGetValue(typeName, out var stats))
            {
                stats.IncrementGets();
            }
        }

        return item;
    }

    /// <summary>
    ///     Returns an instance of T to its pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return<T>(T? obj)
        where T : class
    {
        if (obj != null)
        {
            var pool = GetPool<T>();
            pool.Return(obj);

            if (CollectStatistics)
            {
                var typeName = typeof(T).FullName ?? typeof(T).Name;
                if (PoolStats.TryGetValue(typeName, out var stats))
                {
                    stats.IncrementReturns();
                }
            }
        }
    }

    #endregion

    #region Collection Creators

    /// <summary>
    ///     Creates a new List with the specified initial capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<T> CreateList<T>(int capacity = DefaultObjectCapacity)
    {
        return new List<T>(capacity);
    }

    /// <summary>
    ///     Creates a new Dictionary with the specified initial capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<TKey, TValue> CreateDictionary<TKey, TValue>(
        int capacity = DefaultObjectCapacity,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        return comparer != null
            ? new Dictionary<TKey, TValue>(capacity, comparer)
            : new Dictionary<TKey, TValue>(capacity);
    }

    /// <summary>
    ///     Creates a new HashSet with the specified initial capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashSet<T> CreateHashSet<T>(
        int capacity = DefaultObjectCapacity,
        IEqualityComparer<T>? comparer = null)
    {
        return comparer != null
            ? new HashSet<T>(capacity, comparer)
            : new HashSet<T>(capacity);
    }

    #endregion

    #region Statistics and Monitoring

    /// <summary>
    ///     Gets statistics for all pools.
    /// </summary>
    public static IReadOnlyDictionary<string, PoolStatistics> GetAllStatistics()
    {
        return PoolStats;
    }

    /// <summary>
    ///     Gets statistics for a specific pool type.
    /// </summary>
    public static PoolStatistics? GetStatistics<T>()
        where T : class
    {
        var typeName = typeof(T).FullName ?? typeof(T).Name;
        return PoolStats.TryGetValue(typeName, out var stats) ? stats : null;
    }

    /// <summary>
    ///     Clears all pool statistics.
    /// </summary>
    public static void ClearStatistics()
    {
        foreach (var stats in PoolStats.Values)
        {
            stats.Reset();
        }
    }

    /// <summary>
    ///     Clears all pools and statistics. Use with caution.
    /// </summary>
    public static void ClearAllPools()
    {
        TypedPools.Clear();
        PoolStats.Clear();
    }

    /// <summary>
    ///     Gets a summary of pool health and efficiency.
    /// </summary>
    public static PoolHealthReport GetHealthReport()
    {
        var totalGets = 0L;
        var totalReturns = 0L;
        var inefficientPools = new List<string>();

        foreach (var (typeName, stats) in PoolStats)
        {
            totalGets += stats.TotalGets;
            totalReturns += stats.TotalReturns;

            // Flag pools with poor return rates
            if (stats.ReturnRate < 0.8 && stats.TotalGets > 100)
            {
                inefficientPools.Add($"{typeName} ({stats.ReturnRate:P0})");
            }
        }

        return new PoolHealthReport(
            PoolCount: PoolStats.Count,
            TotalGets: totalGets,
            TotalReturns: totalReturns,
            OverallReturnRate: totalGets > 0 ? (double)totalReturns / totalGets : 1.0,
            InefficientPools: inefficientPools.AsReadOnly());
    }

    #endregion

    #region Policies & Helpers

    /// <summary>
    ///     An IPooledObjectPolicy that uses a user-supplied delegate to create new objects.
    /// </summary>
    private sealed class DelegateObjectPoolPolicy<T> : IPooledObjectPolicy<T>
        where T : class
    {
        private readonly Func<T> _factory;

        public DelegateObjectPoolPolicy(Func<T> factory)
        {
            _factory = factory;
        }

        public T Create()
        {
            return _factory();
        }

        public bool Return(T obj)
        {
            if (obj is IResettable resettable)
            {
                try
                {
                    resettable.Reset();
                }
                catch (Exception ex)
                {
                    // Log but don't throw - just don't return to pool
                    Debug.WriteLine($"Error resetting pooled object: {ex.Message}");
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    ///     A default policy that creates objects via reflection.
    /// </summary>
    private sealed class DefaultObjectPoolPolicy<T> : PooledObjectPolicy<T>
        where T : class
    {
        private readonly ConstructorInfo? _ctor;

        public DefaultObjectPoolPolicy()
        {
            _ctor = typeof(T).GetConstructor(Type.EmptyTypes);
        }

        public override T Create()
        {
            if (_ctor == null)
            {
                throw new InvalidOperationException(
                    $"Type {typeof(T).Name} does not have a public parameterless constructor.");
            }

            return (T)_ctor.Invoke(null)!;
        }

        public override bool Return(T obj)
        {
            if (obj is IResettable resettable)
            {
                try
                {
                    resettable.Reset();
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
    ///     Wrapper pool that tracks statistics.
    /// </summary>
    private sealed class StatisticsTrackingPool<T> : ObjectPool<T>
        where T : class
    {
        private readonly ObjectPool<T> _innerPool;
        private readonly string _typeName;

        public StatisticsTrackingPool(ObjectPool<T> innerPool, string typeName)
        {
            _innerPool = innerPool;
            _typeName = typeName;
        }

        public override T Get()
        {
            return _innerPool.Get();
        }

        public override void Return(T obj)
        {
            _innerPool.Return(obj);
        }
    }

    /// <summary>
    ///     Checks whether the given type has a public parameterless constructor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasParameterlessConstructor(Type type)
    {
        return type.GetConstructor(Type.EmptyTypes) != null;
    }

    #endregion
}

#region Supporting Types

/// <summary>
///     Tracks statistics for an object pool.
/// </summary>
[DebuggerDisplay("Gets = {TotalGets}, Returns = {TotalReturns}, Rate = {ReturnRate:P0}")]
public sealed class PoolStatistics
{
    private long _totalGets;
    private long _totalReturns;

    public PoolStatistics(string typeName)
    {
        TypeName = typeName;
    }

    public string TypeName { get; }
    public long TotalGets => Interlocked.Read(ref _totalGets);
    public long TotalReturns => Interlocked.Read(ref _totalReturns);
    public double ReturnRate => TotalGets > 0 ? (double)TotalReturns / TotalGets : 1.0;
    public long OutstandingObjects => TotalGets - TotalReturns;

    internal void IncrementGets() => Interlocked.Increment(ref _totalGets);
    internal void IncrementReturns() => Interlocked.Increment(ref _totalReturns);

    internal void Reset()
    {
        Interlocked.Exchange(ref _totalGets, 0);
        Interlocked.Exchange(ref _totalReturns, 0);
    }

    public override string ToString()
    {
        return $"{TypeName}: Gets={TotalGets}, Returns={TotalReturns}, " +
               $"Rate={ReturnRate:P0}, Outstanding={OutstandingObjects}";
    }
}

/// <summary>
///     Represents a health report for all object pools.
/// </summary>
public sealed record PoolHealthReport(
    int PoolCount,
    long TotalGets,
    long TotalReturns,
    double OverallReturnRate,
    IReadOnlyList<string> InefficientPools)
{
    public bool IsHealthy => OverallReturnRate >= 0.9 && InefficientPools.Count == 0;

    public string Summary => IsHealthy
        ? $"✓ All {PoolCount} pools healthy ({OverallReturnRate:P0} return rate)"
        : $"⚠ {InefficientPools.Count}/{PoolCount} pools need attention ({OverallReturnRate:P0} return rate)";
}

#endregion
