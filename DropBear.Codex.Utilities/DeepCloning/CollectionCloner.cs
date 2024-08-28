#region

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides methods to clone collections, including handling of arrays and immutable collections.
/// </summary>
public static class CollectionCloner
{
    /// <summary>
    ///     Clones a collection by creating a new instance and populating it with cloned elements.
    /// </summary>
    /// <param name="collection">The source collection to clone.</param>
    /// <param name="collectionType">The type of the collection.</param>
    /// <param name="track">A dictionary to track already cloned objects to avoid circular references.</param>
    /// <returns>An expression that represents the cloned collection.</returns>
    public static Expression CloneCollection(Expression collection, Type collectionType, Expression track)
    {
        var elementType = GetElementType(collectionType);
        var countExpression = GetCountExpression(collection, collectionType);
        var addMethod = GetAddMethod(collectionType);

        var clonedCollection = Expression.Variable(collectionType, "clonedCollection");
        var index = Expression.Variable(typeof(int), "i");
        var loopBreak = Expression.Label("loopBreak");

        var elementClone =
            ExpressionCloner.BuildCloneExpression(elementType, Expression.Property(collection, "Item", index), track);
        var loop = CreateCollectionLoop(collection, countExpression, clonedCollection, addMethod, elementClone, index,
            loopBreak);

        return Expression.Block(new[] { clonedCollection },
            Expression.Assign(clonedCollection, CreateNewInstanceExpression(collectionType)),
            loop,
            clonedCollection);
    }

    /// <summary>
    ///     Clones an array by creating a new instance and populating it with cloned elements.
    /// </summary>
    /// <param name="array">The source array to clone.</param>
    /// <param name="arrayType">The type of the array.</param>
    /// <param name="track">A dictionary to track already cloned objects to avoid circular references.</param>
    /// <returns>An expression that represents the cloned array.</returns>
    public static Expression CloneArray(Expression array, Type arrayType, Expression track)
    {
        var elementType = arrayType.GetElementType() ??
                          throw new InvalidOperationException("Array element type is null.");
        var newArrayExpr = Expression.NewArrayBounds(elementType, Expression.ArrayLength(array));

        return CreateElementCloneLoop(array, newArrayExpr, elementType, track);
    }

    /// <summary>
    ///     Determines if the specified collection type is immutable.
    /// </summary>
    /// <param name="type">The collection type to check.</param>
    /// <returns>True if the collection is immutable; otherwise, false.</returns>
    public static bool IsImmutableCollection(Type type)
    {
        return type.Namespace?.StartsWith("System.Collections.Immutable", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    ///     Clones an immutable collection by creating a new instance and populating it with cloned elements.
    /// </summary>
    /// <param name="collection">The source collection to clone.</param>
    /// <param name="collectionType">The type of the collection.</param>
    /// <param name="track">A dictionary to track already cloned objects to avoid circular references.</param>
    /// <returns>An expression that represents the cloned immutable collection.</returns>
    public static Expression CloneImmutableCollection(Expression collection, Type collectionType, Expression track)
    {
        var elementType = collectionType.GetGenericArguments().First();
        var listType = typeof(List<>).MakeGenericType(elementType);
        var tempListExpr = Expression.Variable(listType, "tempList");

        var toImmutableMethod = collectionType.GetMethod("ToImmutable", BindingFlags.Static | BindingFlags.Public)
                                ?? throw new InvalidOperationException("ToImmutable method not found.");

        return Expression.Block(new[] { tempListExpr },
            Expression.Assign(tempListExpr, Expression.New(listType)),
            PopulateWithClonedElements(collection, tempListExpr, elementType, track),
            Expression.Call(null, toImmutableMethod, tempListExpr));
    }

    /// <summary>
    ///     Creates an expression that instantiates a new instance of the specified type.
    /// </summary>
    /// <param name="type">The type to instantiate.</param>
    /// <returns>An expression that represents the new instance.</returns>
    public static Expression CreateNewInstanceExpression(Type type)
    {
        if (type.IsInterface || type.IsAbstract)
        {
            type = GetConcreteType(type);
        }

        return Expression.New(type);
    }

    private static Expression CreateCollectionLoop(Expression collection, Expression countExpression,
        ParameterExpression clonedCollection, MethodInfo addMethod, Expression elementClone, ParameterExpression index,
        LabelTarget loopBreak)
    {
        return Expression.Block(new[] { index },
            Expression.Assign(index, Expression.Constant(0)),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.LessThan(index, countExpression),
                    Expression.Block(
                        Expression.Call(clonedCollection, addMethod, elementClone),
                        Expression.PostIncrementAssign(index)),
                    Expression.Break(loopBreak)),
                loopBreak));
    }

    private static Expression CreateElementCloneLoop(Expression sourceArray, Expression newArray, Type elementType,
        Expression track)
    {
        var index = Expression.Variable(typeof(int), "index");
        var loopLabel = Expression.Label("loopEnd");

        var clonedElement =
            ExpressionCloner.BuildCloneExpression(elementType, Expression.ArrayIndex(sourceArray, index), track);
        var assignElement = Expression.Assign(Expression.ArrayAccess(newArray, index), clonedElement);

        return Expression.Block(new[] { index },
            Expression.Assign(index, Expression.Constant(0)),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.LessThan(index, Expression.ArrayLength(sourceArray)),
                    Expression.Block(assignElement, Expression.PostIncrementAssign(index)),
                    Expression.Break(loopLabel)),
                loopLabel),
            newArray);
    }

    private static Expression PopulateWithClonedElements(Expression sourceCollection, Expression targetCollection,
        Type elementType, Expression track)
    {
        var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);
        var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");
        var getEnumeratorCall = Expression.Call(sourceCollection,
            typeof(IEnumerable<>).MakeGenericType(elementType).GetMethod("GetEnumerator")
            ?? throw new InvalidOperationException("GetEnumerator method not found."));
        var moveNextCall = Expression.Call(enumeratorVar, typeof(IEnumerator).GetMethod("MoveNext")
                                                          ?? throw new InvalidOperationException(
                                                              "MoveNext method not found."));
        var currentElement = Expression.Property(enumeratorVar, "Current");

        var clonedElement = ExpressionCloner.BuildCloneExpression(elementType, currentElement, track);
        var addCall = Expression.Call(targetCollection, targetCollection.Type.GetMethod("Add")
                                                        ?? throw new InvalidOperationException("Add Method is null."),
            clonedElement);

        return Expression.Block(new[] { enumeratorVar },
            Expression.Assign(enumeratorVar, getEnumeratorCall),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.Equal(moveNextCall, Expression.Constant(true)),
                    addCall,
                    Expression.Break(Expression.Label("loopEnd")))));
    }

    private static Type GetElementType(Type collectionType)
    {
        return collectionType.IsArray
            ? collectionType.GetElementType() ?? throw new InvalidOperationException("Array has no element type.")
            : collectionType.GetGenericArguments().FirstOrDefault()
              ?? throw new InvalidOperationException("Generic collection has no element type.");
    }

    private static Expression GetCountExpression(Expression collection, Type collectionType)
    {
        var countProperty = collectionType.GetProperty("Count")
                            ?? throw new InvalidOperationException(
                                $"No 'Count' property found on type {collectionType.Name}.");

        return Expression.Property(collection, countProperty);
    }

    private static MethodInfo GetAddMethod(Type collectionType)
    {
        return collectionType.GetMethod("Add")
               ?? throw new InvalidOperationException($"No 'Add' method found on type {collectionType.Name}.");
    }

    private static Type GetConcreteType(Type abstractOrInterfaceType)
    {
        var map = new Dictionary<Type, Type>
        {
            { typeof(IEnumerable<>), typeof(List<>) },
            { typeof(ICollection<>), typeof(List<>) },
            { typeof(IList<>), typeof(List<>) },
            { typeof(ISet<>), typeof(HashSet<>) },
            { typeof(IDictionary<,>), typeof(Dictionary<,>) },
            { typeof(IReadOnlyList<>), typeof(List<>) },
            { typeof(IReadOnlyCollection<>), typeof(List<>) },
            { typeof(IReadOnlyDictionary<,>), typeof(ReadOnlyDictionary<,>) },
            { typeof(ConcurrentDictionary<,>), typeof(ConcurrentDictionary<,>) },
            { typeof(ReadOnlyCollection<>), typeof(List<>) },
            { typeof(ReadOnlyDictionary<,>), typeof(Dictionary<,>) }
        };

        if (map.TryGetValue(abstractOrInterfaceType, out var concreteType))
        {
            return concreteType;
        }

        throw new InvalidOperationException(
            $"No concrete type mapped for interface or abstract class {abstractOrInterfaceType.Name}.");
    }
}
