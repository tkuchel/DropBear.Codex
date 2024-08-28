#region

using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using ReferenceEqualityComparer = DropBear.Codex.Utilities.DeepCloning.Comparers.ReferenceEqualityComparer;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides functionality for deep cloning objects using expression trees.
/// </summary>
public static class ExpressionCloner
{
    private static readonly ConcurrentDictionary<Type, Delegate> ClonerCache = new();

    /// <summary>
    ///     Clones the specified object using an expression tree.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="original">The original object to clone.</param>
    /// <returns>A deep clone of the original object.</returns>
    public static T Clone<T>(T original)
    {
        var cloner = GetCloner<T>();
        var track = new Dictionary<object, object>(new ReferenceEqualityComparer());
        return cloner(original, track);
    }

    /// <summary>
    ///     Retrieves or creates a function to clone an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <returns>A function that clones an object of type <typeparamref name="T" />.</returns>
    internal static Func<T, Dictionary<object, object>, T> GetCloner<T>()
    {
        if (ClonerCache.TryGetValue(typeof(T), out var cachedCloner))
        {
            return (Func<T, Dictionary<object, object>, T>)cachedCloner;
        }

        var type = typeof(T);
        var parameter = Expression.Parameter(type, "input");
        var trackParameter = Expression.Parameter(typeof(Dictionary<object, object>), "track");
        var body = BuildCloneExpression(type, parameter, trackParameter);
        var lambda = Expression.Lambda<Func<T, Dictionary<object, object>, T>>(body, parameter, trackParameter);
        var compiled = lambda.Compile();

        ClonerCache.TryAdd(type, compiled);
        return compiled;
    }

    /// <summary>
    ///     Builds the expression tree that defines how to clone an object of the specified type.
    /// </summary>
    /// <param name="type">The type of the object to clone.</param>
    /// <param name="source">The source expression representing the object to clone.</param>
    /// <param name="track">The tracking dictionary to prevent circular references.</param>
    /// <returns>An expression representing the cloning process.</returns>
    internal static Expression BuildCloneExpression(Type type, Expression source, Expression track)
    {
        if (IsImmutable(type))
        {
            return source; // Return the original object for immutable types
        }

        var properties = ReflectionOptimizer.GetProperties(type);
        var bindings = new List<MemberBinding>();

        foreach (var property in properties)
        {
            if (!property.CanWrite || !property.CanRead)
            {
                continue; // Skip properties that cannot be read or written
            }

            var propertyExpression = Expression.Property(source, property);
            var propertyType = property.PropertyType;

            Expression clonedPropertyExpression;

            if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
            {
                clonedPropertyExpression = CollectionCloner.CloneCollection(propertyExpression, propertyType, track);
            }
            else if (!IsImmutable(propertyType))
            {
                clonedPropertyExpression = BuildCloneExpression(propertyType, propertyExpression, track);
            }
            else
            {
                clonedPropertyExpression = propertyExpression;
            }

            bindings.Add(Expression.Bind(property, clonedPropertyExpression));
        }

        return Expression.MemberInit(Expression.New(type), bindings);
    }

    /// <summary>
    ///     Determines whether the specified type is immutable.
    /// </summary>
    /// <param name="type">The type to check for immutability.</param>
    /// <returns><c>true</c> if the type is immutable; otherwise, <c>false</c>.</returns>
    private static bool IsImmutable(Type type)
    {
        if (type.IsPrimitive || type == typeof(string))
        {
            return true; // Basic immutability check for system types
        }

        return type.Namespace?.StartsWith("System.Collections.Immutable", StringComparison.OrdinalIgnoreCase) == true ||
               type.GetProperties().All(prop => prop.SetMethod?.IsPublic != true);
    }
}
