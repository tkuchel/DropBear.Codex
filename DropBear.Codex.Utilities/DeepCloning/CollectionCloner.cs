#region

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides optimized methods to clone collections, including handling of arrays and immutable collections.
///     This class specializes in building expression trees for efficient collection cloning.
/// </summary>
public static class CollectionCloner
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(CollectionCloner));

    // Cache for collection-specific information
    private static readonly ConcurrentDictionary<Type, MethodInfo> AddMethodCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo> CountPropertyCache = new();
    private static readonly ConcurrentDictionary<Type, Type> ConcreteTypeMap = new();

    /// <summary>
    ///     Clones a collection by creating a new instance and populating it with cloned elements.
    /// </summary>
    /// <param name="collection">The source collection to clone.</param>
    /// <param name="collectionType">The type of the collection.</param>
    /// <param name="track">A dictionary to track already cloned objects to avoid circular references.</param>
    /// <param name="currentDepth">Current recursion depth.</param>
    /// <param name="maxDepth">Maximum allowed recursion depth.</param>
    /// <returns>An expression that represents the cloned collection.</returns>
    public static Expression CloneCollection(
        Expression collection,
        Type collectionType,
        Expression track,
        int currentDepth = 0,
        int maxDepth = 32)
    {
        if (currentDepth >= maxDepth)
        {
            Logger.Warning("Maximum cloning depth reached for collection of type {Type}", collectionType.Name);
            return collection; // Return the original at max depth to prevent stack overflow
        }

        try
        {
            var elementType = GetElementType(collectionType);
            var countExpression = GetCountExpression(collection, collectionType);
            var addMethod = GetAddMethod(collectionType);

            // For empty collections, create a new instance and return immediately
            var checkEmptyCollection = Expression.Equal(countExpression, Expression.Constant(0));
            var emptyCollection = Expression.New(GetConcreteType(collectionType));

            // Create temp variable for the collection
            var clonedCollection = Expression.Variable(collectionType, "clonedCollection");
            var index = Expression.Variable(typeof(int), "i");
            var loopBreak = Expression.Label("loopBreak");

            // Get "Item" property accessor
            var itemProperty = collectionType.GetProperty("Item") ??
                               throw new InvalidOperationException(
                                   $"Collection type {collectionType.Name} does not have an indexer");

            // Clone each element
            var sourceElement = Expression.Property(collection, itemProperty, index);
            var elementClone = ExpressionCloner.BuildCloneExpression(
                elementType,
                sourceElement,
                track,
                currentDepth + 1,
                maxDepth);

            // Create the loop to add elements
            var loop = CreateCollectionLoop(
                countExpression,
                clonedCollection,
                addMethod,
                elementClone,
                index,
                loopBreak);

            // Put it all together
            return Expression.Block(
                [clonedCollection, index],
                Expression.IfThenElse(
                    checkEmptyCollection,
                    Expression.Assign(clonedCollection, emptyCollection),
                    Expression.Block(
                        Expression.Assign(clonedCollection, Expression.New(GetConcreteType(collectionType))),
                        loop
                    )
                ),
                clonedCollection);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating expression to clone collection of type {Type}", collectionType.Name);
            throw;
        }
    }

    /// <summary>
    ///     Clones an array by creating a new instance and populating it with cloned elements.
    /// </summary>
    /// <param name="array">The source array to clone.</param>
    /// <param name="arrayType">The type of the array.</param>
    /// <param name="track">A dictionary to track already cloned objects to avoid circular references.</param>
    /// <param name="currentDepth">Current recursion depth.</param>
    /// <param name="maxDepth">Maximum allowed recursion depth.</param>
    /// <returns>An expression that represents the cloned array.</returns>
    public static Expression CloneArray(
        Expression array,
        Type arrayType,
        Expression track,
        int currentDepth = 0,
        int maxDepth = 32)
    {
        if (currentDepth >= maxDepth)
        {
            Logger.Warning("Maximum cloning depth reached for array of type {Type}", arrayType.Name);
            return array; // Return the original at max depth to prevent stack overflow
        }

        try
        {
            var elementType = arrayType.GetElementType() ??
                              throw new InvalidOperationException("Array element type is null.");

            // Handle primitive element types efficiently using Array.Clone()
            if (elementType.IsPrimitive || elementType == typeof(string))
            {
                // Use Array.Clone() which does a shallow copy
                return Expression.Call(array, typeof(Array).GetMethod("Clone")!);
            }

            // Create a new array with the same length
            var arrayLength = Expression.ArrayLength(array);
            var newArray = Expression.NewArrayBounds(elementType, arrayLength);

            // Check if it's an empty array
            var isEmptyArray = Expression.Equal(arrayLength, Expression.Constant(0));

            // For non-empty arrays, clone each element
            return Expression.Condition(
                isEmptyArray,
                newArray,
                CreateElementCloneLoop(array, newArray, elementType, track, currentDepth + 1, maxDepth)
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating expression to clone array of type {Type}", arrayType.Name);
            throw;
        }
    }

    /// <summary>
    ///     Determines if the specified collection type is immutable.
    /// </summary>
    /// <param name="type">The collection type to check.</param>
    /// <returns>True if the collection is immutable; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsImmutableCollection(Type type)
    {
        // Check namespace for System.Collections.Immutable
        if (type.Namespace?.StartsWith("System.Collections.Immutable", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        // Check for read-only collection types
        return type == typeof(ReadOnlyCollection<>) ||
               type == typeof(ReadOnlyDictionary<,>) ||
               type == typeof(ReadOnlySpan<>) ||
               type == typeof(ReadOnlyMemory<>);
    }

    /// <summary>
    ///     Clones an immutable collection by creating a new instance and populating it with cloned elements.
    /// </summary>
    /// <param name="collection">The source collection to clone.</param>
    /// <param name="collectionType">The type of the collection.</param>
    /// <param name="track">A dictionary to track already cloned objects to avoid circular references.</param>
    /// <param name="currentDepth">Current recursion depth.</param>
    /// <param name="maxDepth">Maximum allowed recursion depth.</param>
    /// <returns>An expression that represents the cloned immutable collection.</returns>
    public static Expression CloneImmutableCollection(
        Expression collection,
        Type collectionType,
        Expression track,
        int currentDepth = 0,
        int maxDepth = 32)
    {
        if (currentDepth >= maxDepth)
        {
            Logger.Warning("Maximum cloning depth reached for immutable collection of type {Type}",
                collectionType.Name);
            return collection; // Return the original at max depth
        }

        try
        {
            var elementType = collectionType.GetGenericArguments().FirstOrDefault() ??
                              throw new InvalidOperationException(
                                  $"Cannot determine element type for {collectionType.Name}");

            // Create a temporary list to hold cloned elements
            var listType = typeof(List<>).MakeGenericType(elementType);
            var tempListExpr = Expression.Variable(listType, "tempList");

            // Find the ToImmutable method
            var toImmutableMethod =
                collectionType.GetMethod("ToImmutable", BindingFlags.Static | BindingFlags.Public) ??
                throw new InvalidOperationException($"ToImmutable method not found on type {collectionType.Name}");

            // Create and populate the temp list
            return Expression.Block(
                [tempListExpr],
                Expression.Assign(tempListExpr, Expression.New(listType)),
                PopulateWithClonedElements(collection, tempListExpr, elementType, track, currentDepth + 1, maxDepth),
                Expression.Call(null, toImmutableMethod, tempListExpr)
            );
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating expression to clone immutable collection of type {Type}",
                collectionType.Name);
            throw;
        }
    }

    /// <summary>
    ///     Creates an expression that instantiates a new instance of the specified type.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <returns>An expression that represents the new instance.</returns>
    public static Expression CreateNewInstanceExpression(Type type)
    {
        // If it's an interface or abstract class, get a concrete type
        var concreteType = type.IsInterface || type.IsAbstract
            ? GetConcreteType(type)
            : type;

        // Find a parameterless constructor
        var constructor = concreteType.GetConstructor(Type.EmptyTypes);

        if (constructor == null)
        {
            throw new InvalidOperationException($"Type {concreteType.Name} does not have a parameterless constructor");
        }

        return Expression.New(constructor);
    }

    /// <summary>
    ///     Creates a loop that iterates through a collection and adds cloned elements to a new collection.
    /// </summary>
    private static Expression CreateCollectionLoop(
        Expression countExpression,
        ParameterExpression clonedCollection,
        MethodInfo addMethod,
        Expression elementClone,
        ParameterExpression index,
        LabelTarget loopBreak)
    {
        // Initialize index to 0
        var initIndex = Expression.Assign(index, Expression.Constant(0));

        // The loop condition
        var condition = Expression.LessThan(index, countExpression);

        // The loop body: add the cloned element and increment index
        var loopBody = Expression.Block(
            Expression.Call(clonedCollection, addMethod, elementClone),
            Expression.PostIncrementAssign(index)
        );

        // Create the loop
        return Expression.Block(
            initIndex,
            Expression.Loop(
                Expression.IfThenElse(
                    condition,
                    loopBody,
                    Expression.Break(loopBreak)
                ),
                loopBreak
            )
        );
    }

    /// <summary>
    ///     Creates a loop that iterates through an array and assigns cloned elements to a new array.
    /// </summary>
    private static Expression CreateElementCloneLoop(
        Expression sourceArray,
        Expression newArray,
        Type elementType,
        Expression track,
        int currentDepth,
        int maxDepth)
    {
        // Create index variable
        var index = Expression.Variable(typeof(int), "index");
        var loopLabel = Expression.Label("loopEnd");

        // Get the element from the source array
        var sourceElement = Expression.ArrayIndex(sourceArray, index);

        // Clone the element
        var clonedElement = ExpressionCloner.BuildCloneExpression(
            elementType,
            sourceElement,
            track,
            currentDepth,
            maxDepth);

        // Assign the cloned element to the new array
        var assignElement = Expression.Assign(
            Expression.ArrayAccess(newArray, index),
            clonedElement);

        // Create the loop
        return Expression.Block(
            [index],
            Expression.Assign(index, Expression.Constant(0)),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.LessThan(index, Expression.ArrayLength(sourceArray)),
                    Expression.Block(
                        assignElement,
                        Expression.PostIncrementAssign(index)
                    ),
                    Expression.Break(loopLabel)
                ),
                loopLabel
            ),
            newArray
        );
    }

    /// <summary>
    ///     Creates an expression that populates a collection with cloned elements from a source collection.
    /// </summary>
    private static Expression PopulateWithClonedElements(
        Expression sourceCollection,
        Expression targetCollection,
        Type elementType,
        Expression track,
        int currentDepth,
        int maxDepth)
    {
        // Create variables for enumerator
        var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);
        var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");

        // Get the enumerator from the source collection
        var getEnumeratorMethod = typeof(IEnumerable<>)
                                      .MakeGenericType(elementType)
                                      .GetMethod("GetEnumerator") ??
                                  throw new InvalidOperationException("GetEnumerator method not found");

        var getEnumeratorCall = Expression.Call(sourceCollection, getEnumeratorMethod);

        // Get MoveNext and Current from the enumerator
        var moveNextMethod = typeof(IEnumerator).GetMethod("MoveNext") ??
                             throw new InvalidOperationException("MoveNext method not found");

        var moveNextCall = Expression.Call(enumeratorVar, moveNextMethod);
        var currentProperty = enumeratorType.GetProperty("Current") ??
                              throw new InvalidOperationException("Current property not found");

        var currentElement = Expression.Property(enumeratorVar, currentProperty);

        // Clone the current element
        var clonedElement = ExpressionCloner.BuildCloneExpression(
            elementType,
            currentElement,
            track,
            currentDepth,
            maxDepth);

        // Add the cloned element to the target collection
        var addMethod = targetCollection.Type.GetMethod("Add") ??
                        throw new InvalidOperationException("Add method not found");

        var addCall = Expression.Call(targetCollection, addMethod, clonedElement);

        // Create the loop
        var loopEnd = Expression.Label("loopEnd");

        return Expression.Block(
            [enumeratorVar],
            Expression.Assign(enumeratorVar, getEnumeratorCall),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.Equal(moveNextCall, Expression.Constant(true)),
                    addCall,
                    Expression.Break(loopEnd)
                ),
                loopEnd
            )
        );
    }

    /// <summary>
    ///     Gets the element type of a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Type GetElementType(Type collectionType)
    {
        // Try array element type first
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType() ??
                   throw new InvalidOperationException("Array has no element type");
        }

        // Try generic arguments for generic collections
        if (collectionType.IsGenericType)
        {
            var args = collectionType.GetGenericArguments();

            // Handle dictionaries and key-value pairs specially
            if (typeof(IDictionary).IsAssignableFrom(collectionType) && args.Length >= 2)
            {
                return typeof(KeyValuePair<,>).MakeGenericType(args[0], args[1]);
            }

            // For other generic collections, return the first generic argument
            return args.FirstOrDefault() ??
                   throw new InvalidOperationException("Generic collection has no element type");
        }

        // For non-generic collections, try to infer element type from interfaces
        foreach (var iface in collectionType.GetInterfaces())
        {
            if (iface.IsGenericType)
            {
                var genericType = iface.GetGenericTypeDefinition();

                if (genericType == typeof(IEnumerable<>) ||
                    genericType == typeof(ICollection<>) ||
                    genericType == typeof(IList<>))
                {
                    return iface.GetGenericArguments()[0];
                }
            }
        }

        // Fallback to object for non-generic collections
        return typeof(object);
    }

    /// <summary>
    ///     Gets an expression that represents the count of a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Expression GetCountExpression(Expression collection, Type collectionType)
    {
        // Use cached property info if available
        var countProperty = CountPropertyCache.GetOrAdd(collectionType, type =>
        {
            // Try Count property first
            var prop = type.GetProperty("Count");

            // Fallback to Length for arrays and similar
            if (prop == null)
            {
                prop = type.GetProperty("Length");
            }

            if (prop == null)
            {
                throw new InvalidOperationException($"No 'Count' or 'Length' property found on type {type.Name}");
            }

            return prop;
        });

        return Expression.Property(collection, countProperty);
    }

    /// <summary>
    ///     Gets the Add method of a collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MethodInfo GetAddMethod(Type collectionType)
    {
        // Use cached method info if available
        return AddMethodCache.GetOrAdd(collectionType, type =>
        {
            var method = type.GetMethod("Add");

            if (method == null)
            {
                throw new InvalidOperationException($"No 'Add' method found on type {type.Name}");
            }

            return method;
        });
    }

    /// <summary>
    ///     Gets a concrete type for an interface or abstract class.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Type GetConcreteType(Type abstractOrInterfaceType)
    {
        // If not an interface or abstract class, return the type itself
        if (!abstractOrInterfaceType.IsInterface && !abstractOrInterfaceType.IsAbstract)
        {
            return abstractOrInterfaceType;
        }

        // Use cached mapping if available
        return ConcreteTypeMap.GetOrAdd(abstractOrInterfaceType, type =>
        {
            // Handle generic types
            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();

                // Define mappings from interface types to concrete implementations
                var map = new Dictionary<Type, Type>
                {
                    { typeof(IEnumerable<>), typeof(List<>) },
                    { typeof(ICollection<>), typeof(List<>) },
                    { typeof(IList<>), typeof(List<>) },
                    { typeof(ISet<>), typeof(HashSet<>) },
                    { typeof(IDictionary<,>), typeof(Dictionary<,>) },
                    { typeof(IReadOnlyList<>), typeof(List<>) },
                    { typeof(IReadOnlyCollection<>), typeof(List<>) },
                    { typeof(IReadOnlyDictionary<,>), typeof(ReadOnlyDictionary<,>) }
                };

                if (map.TryGetValue(genericTypeDefinition, out var concreteGenericType))
                {
                    // Make the concrete generic type with the same type arguments
                    return concreteGenericType.MakeGenericType(genericArgs);
                }
            }
            else
            {
                // Handle non-generic types
                var map = new Dictionary<Type, Type>
                {
                    { typeof(IEnumerable), typeof(ArrayList) },
                    { typeof(ICollection), typeof(ArrayList) },
                    { typeof(IList), typeof(ArrayList) },
                    { typeof(IDictionary), typeof(Hashtable) }
                };

                if (map.TryGetValue(type, out var concreteType))
                {
                    return concreteType;
                }
            }

            throw new InvalidOperationException(
                $"No concrete type mapped for interface or abstract class {type.Name}");
        });
    }

    /// <summary>
    ///     Clears all collection-related caches.
    /// </summary>
    public static void ClearCaches()
    {
        AddMethodCache.Clear();
        CountPropertyCache.Clear();
        ConcreteTypeMap.Clear();

        Logger.Information("Collection cloner caches cleared");
    }
}
