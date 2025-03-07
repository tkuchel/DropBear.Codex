#region

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

#endregion

namespace DropBear.Codex.Core.Common;

/// <summary>
///     Provides a unified object pooling infrastructure for DropBear.Codex.
///     By default, if a type <typeparamref name="T" /> has a public parameterless constructor,
///     this class will instantiate via reflection. If it does not, an exception is thrown
///     unless the caller supplies a delegate factory.
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
    ///     The global <see cref="DefaultObjectPoolProvider" /> used to create individual pools.
    /// </summary>
    private static readonly DefaultObjectPoolProvider Provider = new()
    {
        // Up to this many objects can be retained in each pool
        MaximumRetained = DefaultPoolSize
    };

    /// <summary>
    ///     A cache of <see cref="ObjectPool{T}" /> instances keyed by type name, ensuring each
    ///     type is associated with only one shared pool.
    /// </summary>
    private static readonly ConcurrentDictionary<string, object> TypedPools = new(StringComparer.Ordinal);

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
    ///     Creates or retrieves a shared <see cref="ObjectPool{T}" /> for the specified type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type of objects to pool.</typeparam>
    /// <param name="factory">
    ///     An optional delegate used to create new instances of <typeparamref name="T" />. If omitted,
    ///     this method will attempt to instantiate <typeparamref name="T" /> via reflection on a public
    ///     parameterless constructor. If none is found, an exception is thrown.
    /// </param>
    /// <returns>An <see cref="ObjectPool{T}" /> instance associated with type <typeparamref name="T" />.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if no factory is provided and <typeparamref name="T" /> lacks a public parameterless constructor.
    /// </exception>
    public static ObjectPool<T> GetPool<T>(Func<T>? factory = null)
        where T : class
    {
        // Attempt to reuse a previously created pool, if any.
        var typeName = typeof(T).FullName ?? typeof(T).Name;
        if (TypedPools.TryGetValue(typeName, out var existingPool) &&
            existingPool is ObjectPool<T> typedPool)
        {
            return typedPool;
        }

        // No cached pool, so we must create a new one.
        ObjectPool<T> newPool;

        if (factory == null)
        {
            // If no factory was provided, check whether T has a public parameterless constructor.
            if (HasParameterlessConstructor(typeof(T)))
            {
                // We can instantiate T using reflection inside our default policy.
                var policy = new DefaultObjectPoolPolicy<T>();
                newPool = Provider.Create(policy);
            }
            else
            {
                // T does not have a parameterless constructor, and no custom factory was supplied.
                throw new InvalidOperationException(
                    $"Cannot create an object pool for type {typeof(T).Name} " +
                    "because it lacks a public parameterless constructor. Provide a factory delegate instead."
                );
            }
        }
        else
        {
            // A custom factory was supplied, so we use the delegate-based policy.
            var policy = new DelegateObjectPoolPolicy<T>(factory);
            newPool = Provider.Create(policy);
        }

        // Cache the new pool for future requests.
        TypedPools.TryAdd(typeName, newPool);
        return newPool;
    }

    /// <summary>
    ///     Retrieves one object of type <typeparamref name="T" /> from the pool (or instantiates if the pool is empty).
    /// </summary>
    /// <typeparam name="T">The type of object to fetch.</typeparam>
    /// <returns>An instance of <typeparamref name="T" /> from the associated pool.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Get<T>()
        where T : class
    {
        return GetPool<T>().Get();
    }

    /// <summary>
    ///     Returns an instance of <typeparamref name="T" /> to its pool, allowing reuse later.
    /// </summary>
    /// <typeparam name="T">The type of the returned object.</typeparam>
    /// <param name="obj">The instance to return to the pool (ignored if null).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return<T>(T? obj)
        where T : class
    {
        if (obj != null)
        {
            GetPool<T>().Return(obj);
        }
    }

    #endregion

    #region Collection Creators

    /// <summary>
    ///     Creates a new <see cref="List{T}" /> using the specified initial capacity.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="capacity">An optional initial capacity (default is 32).</param>
    /// <returns>A new <see cref="List{T}" /> instance.</returns>
    public static List<T> CreateList<T>(int capacity = DefaultObjectCapacity)
    {
        return new List<T>(capacity);
    }

    /// <summary>
    ///     Creates a new <see cref="Dictionary{TKey,TValue}" /> using the specified initial capacity
    ///     and optionally a custom key comparer.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="capacity">An optional initial capacity (default is 32).</param>
    /// <param name="comparer">An optional key comparer.</param>
    /// <returns>A new <see cref="Dictionary{TKey,TValue}" /> instance.</returns>
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
    ///     Creates a new <see cref="HashSet{T}" /> using the specified initial capacity
    ///     and optionally a custom element comparer.
    /// </summary>
    /// <typeparam name="T">The type of elements in the hash set.</typeparam>
    /// <param name="capacity">An optional initial capacity (default is 32).</param>
    /// <param name="comparer">An optional element comparer.</param>
    /// <returns>A new <see cref="HashSet{T}" /> instance.</returns>
    public static HashSet<T> CreateHashSet<T>(
        int capacity = DefaultObjectCapacity,
        IEqualityComparer<T>? comparer = null)
    {
        return comparer != null
            ? new HashSet<T>(capacity, comparer)
            : new HashSet<T>(capacity);
    }

    #endregion

    #region Policies & Helpers

    /// <summary>
    ///     An <see cref="IPooledObjectPolicy{T}" /> that uses a user-supplied delegate to create new objects.
    /// </summary>
    private sealed class DelegateObjectPoolPolicy<T> : IPooledObjectPolicy<T>
        where T : class
    {
        private readonly Func<T> _factory;

        /// <summary>
        ///     Constructs a policy that creates objects by invoking the given <paramref name="factory" />.
        /// </summary>
        /// <param name="factory">A delegate for creating <typeparamref name="T" /> instances.</param>
        public DelegateObjectPoolPolicy(Func<T> factory)
        {
            _factory = factory;
        }

        /// <inheritdoc />
        public T Create()
        {
            return _factory();
        }

        /// <inheritdoc />
        public bool Return(T obj)
        {
            if (obj is IResettable resettable)
            {
                resettable.Reset();
            }

            return true;
        }
    }

    /// <summary>
    ///     A default policy that attempts to create objects via reflection on a public, parameterless constructor.
    /// </summary>
    private sealed class DefaultObjectPoolPolicy<T> : PooledObjectPolicy<T>
        where T : class
    {
        private readonly ConstructorInfo? _ctor;

        /// <summary>
        ///     Initializes a new instance of the <see cref="DefaultObjectPoolPolicy{T}" />.
        ///     If <typeparamref name="T" /> doesn't have a public parameterless constructor,
        ///     this policy will throw at <see cref="Create" />.
        /// </summary>
        public DefaultObjectPoolPolicy()
        {
            _ctor = typeof(T).GetConstructor(Type.EmptyTypes);
        }

        /// <inheritdoc />
        public override T Create()
        {
            if (_ctor == null)
            {
                throw new InvalidOperationException(
                    $"Type {typeof(T).Name} does not have a public parameterless constructor. " +
                    "Provide a custom factory or use a specialized policy."
                );
            }

            // We assume the constructor is valid.
            return (T)_ctor.Invoke(null)!;
        }

        /// <inheritdoc />
        public override bool Return(T obj)
        {
            if (obj is IResettable resettable)
            {
                resettable.Reset();
            }

            return true;
        }
    }

    /// <summary>
    ///     Checks whether the given <paramref name="type" /> has a public parameterless constructor.
    /// </summary>
    /// <param name="type">The type to inspect for a parameterless constructor.</param>
    /// <returns><c>true</c> if <paramref name="type" /> has a public parameterless constructor; otherwise <c>false</c>.</returns>
    private static bool HasParameterlessConstructor(Type type)
    {
        return type.GetConstructor(Type.EmptyTypes) != null;
    }

    #endregion
}
