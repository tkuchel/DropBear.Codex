#region

using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Utilities.DeepCloning.Attributes;
using Serilog;
using ReferenceEqualityComparer = DropBear.Codex.Utilities.DeepCloning.Comparers.ReferenceEqualityComparer;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides functionality for deep cloning objects using expression trees.
///     Optimized for performance and memory efficiency.
/// </summary>
public static class ExpressionCloner
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(ExpressionCloner));

    // Cache for compiled cloner functions
    private static readonly ConcurrentDictionary<Type, Delegate> ClonerCache = new();

    // Cache for property accessor delegates
    private static readonly ConcurrentDictionary<PropertyInfo, Delegate> PropertyGetterCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, Delegate> PropertySetterCache = new();

    // Cache for type information to avoid repeated lookups
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> TypePropertiesCache = new();
    private static readonly ConcurrentDictionary<Type, bool> ImmutableTypesCache = new();

    /// <summary>
    ///     Clones the specified object using an expression tree.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="original">The original object to clone.</param>
    /// <returns>A deep clone of the original object.</returns>
    public static T Clone<T>(T original) where T : class
    {
        if (original == null)
        {
            return null!;
        }

        // Fast path for immutable types
        if (IsImmutableType(typeof(T)))
        {
            return original;
        }

        var cloner = GetCloner<T>();
        var track = new Dictionary<object, object>(new ReferenceEqualityComparer());
        return cloner(original, track);
    }

    /// <summary>
    ///     Retrieves or creates a function to clone an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <returns>A function that clones an object of type <typeparamref name="T" />.</returns>
    internal static Func<T, Dictionary<object, object>, T> GetCloner<T>() where T : class
    {
        if (ClonerCache.TryGetValue(typeof(T), out var cachedCloner))
        {
            return (Func<T, Dictionary<object, object>, T>)cachedCloner;
        }

        Logger.Debug("Building cloner expression for type {Type}", typeof(T).Name);

        var type = typeof(T);
        var parameter = Expression.Parameter(type, "input");
        var trackParameter = Expression.Parameter(typeof(Dictionary<object, object>), "track");

        // Create labels for circular reference check
        var returnTarget = Expression.Label(type);
        var circularRefCheckLabel = Expression.Label();

        // Check for circular references
        var circularRefKey = Expression.Constant("CircularRefKey");
        var trackContainsCheck = Expression.Call(
            trackParameter,
            typeof(Dictionary<object, object>).GetMethod("ContainsKey")!,
            parameter);

        var circularRefCheck = Expression.IfThen(
            trackContainsCheck,
            Expression.Return(returnTarget,
                Expression.Convert(
                    Expression.Call(
                        trackParameter,
                        typeof(Dictionary<object, object>).GetMethod("get_Item")!,
                        parameter),
                    type)));

        // Track this object
        var trackAddStatement = Expression.Call(
            trackParameter,
            typeof(Dictionary<object, object>).GetMethod("Add")!,
            parameter,
            Expression.Variable(typeof(object), "placeholder"));

        // Build the clone expression
        var cloneExpr = BuildCloneExpression(type, parameter, trackParameter);

        // Save cloned object in tracking dictionary
        var trackSetResultStatement = Expression.Call(
            trackParameter,
            typeof(Dictionary<object, object>).GetMethod("set_Item")!,
            parameter,
            Expression.Convert(cloneExpr, typeof(object)));

        // Create and compile the full expression
        var body = Expression.Block(
            circularRefCheck,
            trackAddStatement,
            trackSetResultStatement,
            Expression.Label(returnTarget, cloneExpr));

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
    /// <param name="currentDepth">Current recursion depth (default is 0).</param>
    /// <param name="maxDepth">Maximum allowed recursion depth (default is 32).</param>
    /// <returns>An expression representing the cloning process.</returns>
    internal static Expression BuildCloneExpression(
        Type type,
        Expression source,
        Expression track,
        int currentDepth = 0,
        int maxDepth = 32)
    {
        // Fast path for immutable types
        if (IsImmutableType(type))
        {
            return source;
        }

        // Prevent stack overflow with deep hierarchies
        if (currentDepth >= maxDepth)
        {
            Logger.Warning("Maximum cloning depth reached ({Depth}) for type {Type}", currentDepth, type.Name);
            return source; // Return the original to avoid going deeper
        }

        // Handle array types specially
        if (type.IsArray)
        {
            return CollectionCloner.CloneArray(source, type, track, currentDepth + 1, maxDepth);
        }

        // Handle collection types
        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            // Dictionary types
            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                return CollectionCloner.CloneCollection(source, type, track, currentDepth + 1, maxDepth);
            }

            // Other collection types
            if (CollectionCloner.IsImmutableCollection(type))
            {
                return CollectionCloner.CloneImmutableCollection(source, type, track, currentDepth + 1, maxDepth);
            }

            return CollectionCloner.CloneCollection(source, type, track, currentDepth + 1, maxDepth);
        }

        // For other types, create a new instance and copy properties
        var properties = GetTypeProperties(type);
        var bindings = new List<MemberBinding>();

        foreach (var property in properties)
        {
            // Skip properties that can't be read or written
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            try
            {
                // Check for DontCloneAttribute
                var dontCloneAttr = property.GetCustomAttribute<DontCloneAttribute>();
                if (dontCloneAttr != null)
                {
                    continue;
                }

                // Check for CloneableAttribute with IsCloneable=false
                var cloneableAttr = property.GetCustomAttribute<CloneableAttribute>();
                if (cloneableAttr != null && !cloneableAttr.IsCloneable)
                {
                    continue;
                }

                var propertyExpression = Expression.Property(source, property);
                var propertyType = property.PropertyType;

                Expression clonedPropertyExpression;

                // Check for ShallowCopyOnly in CloneableAttribute
                if (cloneableAttr != null && cloneableAttr.ShallowCopyOnly)
                {
                    clonedPropertyExpression = propertyExpression;
                }
                else
                {
                    // Handle null check for reference types
                    if (!propertyType.IsValueType)
                    {
                        var nullCheck = Expression.Equal(propertyExpression, Expression.Constant(null));
                        var nullValue = Expression.Constant(null, propertyType);
                        var nonNullValue = BuildCloneExpression(
                            propertyType, propertyExpression, track, currentDepth + 1, maxDepth);

                        clonedPropertyExpression = Expression.Condition(nullCheck, nullValue, nonNullValue);
                    }
                    else
                    {
                        clonedPropertyExpression = BuildCloneExpression(
                            propertyType, propertyExpression, track, currentDepth + 1, maxDepth);
                    }
                }

                bindings.Add(Expression.Bind(property, clonedPropertyExpression));
            }
            catch (Exception ex)
            {
                // Log but continue with other properties
                Logger.Warning(ex, "Error creating binding for property {Property} on type {Type}",
                    property.Name, type.Name);
            }
        }

        var newExpression = Expression.New(type);
        return Expression.MemberInit(newExpression, bindings);
    }


    /// <summary>
    ///     Creates an expression to clone an array.
    /// </summary>
    private static Expression CloneArray(Expression source, Type arrayType, Expression track)
    {
        var elementType = arrayType.GetElementType()!;

        // Create a new array of the same length
        var arrayLength = Expression.ArrayLength(source);
        var newArray = Expression.NewArrayBounds(elementType, arrayLength);

        // For primitive/immutable element types, use Array.Copy for better performance
        if (IsImmutableType(elementType))
        {
            return Expression.Block(
                newArray,
                Expression.Call(
                    typeof(Array).GetMethod("Copy", new[] { typeof(Array), typeof(Array), typeof(int) })!,
                    source,
                    newArray,
                    arrayLength),
                newArray);
        }

        // For complex element types, clone each element
        var index = Expression.Variable(typeof(int), "index");
        var loopTarget = Expression.Label("loopEnd");

        var clonedElement = BuildCloneExpression(
            elementType,
            Expression.ArrayAccess(source, index),
            track);

        var assignElement = Expression.Assign(
            Expression.ArrayAccess(newArray, index),
            clonedElement);

        // Create a loop to process each element
        var loopBody = Expression.Block(
            assignElement,
            Expression.PostIncrementAssign(index));

        var condition = Expression.LessThan(index, arrayLength);

        var loop = Expression.Block(
            new[] { index },
            Expression.Assign(index, Expression.Constant(0)),
            Expression.Loop(
                Expression.IfThenElse(
                    condition,
                    loopBody,
                    Expression.Break(loopTarget)),
                loopTarget));

        return Expression.Block(newArray, loop, newArray);
    }

    /// <summary>
    ///     Gets cached properties for a type.
    /// </summary>
    private static PropertyInfo[] GetTypeProperties(Type type)
    {
        return TypePropertiesCache.GetOrAdd(type, t =>
        {
            var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .ToArray();

            Logger.Debug("Cached {Count} properties for type {Type}", properties.Length, t.Name);
            return properties;
        });
    }

    /// <summary>
    ///     Gets a property getter delegate from cache or creates a new one.
    /// </summary>
    private static Func<object, object> GetPropertyGetter(PropertyInfo property)
    {
        return (Func<object, object>)PropertyGetterCache.GetOrAdd(property, prop =>
        {
            // Create optimized property accessor
            var instance = Expression.Parameter(typeof(object), "instance");
            var convertedInstance = Expression.Convert(instance, prop.DeclaringType!);
            var propertyAccess = Expression.Property(convertedInstance, prop);
            var convertResult = Expression.Convert(propertyAccess, typeof(object));

            return Expression.Lambda<Func<object, object>>(convertResult, instance).Compile();
        });
    }

    /// <summary>
    ///     Gets a property setter delegate from cache or creates a new one.
    /// </summary>
    private static Action<object, object> GetPropertySetter(PropertyInfo property)
    {
        return (Action<object, object>)PropertySetterCache.GetOrAdd(property, prop =>
        {
            // Create optimized property setter
            var instance = Expression.Parameter(typeof(object), "instance");
            var value = Expression.Parameter(typeof(object), "value");
            var convertedInstance = Expression.Convert(instance, prop.DeclaringType!);
            var convertedValue = Expression.Convert(value, prop.PropertyType);
            var propertyAccess = Expression.Property(convertedInstance, prop);
            var assignment = Expression.Assign(propertyAccess, convertedValue);

            return Expression.Lambda<Action<object, object>>(assignment, instance, value).Compile();
        });
    }

    /// <summary>
    ///     Determines if a type is immutable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsImmutableType(Type type)
    {
        return ImmutableTypesCache.GetOrAdd(type, t =>
        {
            // Primitive types, strings, and other known immutable types
            if (t.IsPrimitive || t == typeof(string) || t == typeof(decimal) ||
                t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(TimeSpan) ||
                t == typeof(Guid) || t.IsEnum || t == typeof(Type))
            {
                return true;
            }

            // Check for .NET immutable collections
            if (t.Namespace?.StartsWith("System.Collections.Immutable", StringComparison.Ordinal) == true)
            {
                return true;
            }

            // Consider a type immutable if all properties have only getters
            var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            return properties.Length > 0 && properties.All(p => p.GetSetMethod(true) == null);
        });
    }

    /// <summary>
    ///     Creates a fast shallow clone of an object.
    /// </summary>
    public static T ShallowClone<T>(T original) where T : class
    {
        if (original == null)
        {
            return null!;
        }

        // Fast path for immutable types
        if (IsImmutableType(typeof(T)))
        {
            return original;
        }

        try
        {
            // Try to use MemberwiseClone via reflection for better performance
            var memberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (memberwiseCloneMethod != null)
            {
                return (T)memberwiseCloneMethod.Invoke(original, null)!;
            }

            // Fallback to manual property copying
            var instance = Activator.CreateInstance<T>();
            var properties = GetTypeProperties(typeof(T));

            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    var value = prop.GetValue(original);
                    prop.SetValue(instance, value);
                }
            }

            return instance;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during shallow clone");
            throw new InvalidOperationException("Failed to create shallow clone", ex);
        }
    }
}
