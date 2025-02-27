#region

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Utilities.DeepCloning.Attributes;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides optimized access to reflection-based information such as fields and properties using caching mechanisms.
///     This class improves performance by avoiding repeated reflection operations on the same types.
/// </summary>
public static class ReflectionOptimizer
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(ReflectionOptimizer));

    // Cache collections for different reflection information
    private static readonly ConcurrentDictionary<Type, ReadOnlyCollection<FieldInfo>> FieldsCache = new();
    private static readonly ConcurrentDictionary<Type, ReadOnlyCollection<PropertyInfo>> PropertiesCache = new();
    private static readonly ConcurrentDictionary<Type, ReadOnlyCollection<ConstructorInfo>> ConstructorsCache = new();
    private static readonly ConcurrentDictionary<Type, ReadOnlyCollection<MethodInfo>> MethodsCache = new();

    // Cache for members with CloneableAttribute
    private static readonly ConcurrentDictionary<Type, ReadOnlyCollection<MemberInfo>> CloneableMembersCache = new();

    // Cache for determining if a type has a parameterless constructor
    private static readonly ConcurrentDictionary<Type, bool> HasParameterlessConstructorCache = new();

    /// <summary>
    ///     Retrieves the fields of the specified type, utilizing caching for performance.
    /// </summary>
    /// <param name="type">The type whose fields are to be retrieved.</param>
    /// <param name="bindingFlags">Optional binding flags to customize the field retrieval.</param>
    /// <returns>A read-only collection of <see cref="FieldInfo" /> objects representing the fields of the specified type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyCollection<FieldInfo> GetFields(Type type,
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
    {
        var cacheKey = new Tuple<Type, BindingFlags>(type, bindingFlags);
        return FieldsCache.GetOrAdd(type, _ =>
        {
            var fields = type.GetFields(bindingFlags);
            return new ReadOnlyCollection<FieldInfo>(fields);
        });
    }

    /// <summary>
    ///     Retrieves the properties of the specified type, utilizing caching for performance.
    /// </summary>
    /// <param name="type">The type whose properties are to be retrieved.</param>
    /// <param name="bindingFlags">Optional binding flags to customize the property retrieval.</param>
    /// <returns>
    ///     A read-only collection of <see cref="PropertyInfo" /> objects representing the properties of the specified
    ///     type.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyCollection<PropertyInfo> GetProperties(Type type,
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
    {
        return PropertiesCache.GetOrAdd(type, _ =>
        {
            var properties = type.GetProperties(bindingFlags);
            return new ReadOnlyCollection<PropertyInfo>(properties);
        });
    }

    /// <summary>
    ///     Retrieves the cloneable members (fields and properties) of the specified type.
    /// </summary>
    /// <param name="type">The type whose cloneable members are to be retrieved.</param>
    /// <returns>A read-only collection of <see cref="MemberInfo" /> objects representing the cloneable members.</returns>
    /// <remarks>
    ///     A member is considered cloneable if it has the <see cref="CloneableAttribute" /> with IsCloneable set to true,
    ///     or if it doesn't have the attribute at all.
    /// </remarks>
    public static ReadOnlyCollection<MemberInfo> GetCloneableMembers(Type type)
    {
        return CloneableMembersCache.GetOrAdd(type, t =>
        {
            var members = new List<MemberInfo>();

            // Get fields
            foreach (var field in GetFields(t))
            {
                var attribute = field.GetCustomAttribute<CloneableAttribute>();
                if (attribute == null || attribute.IsCloneable)
                {
                    members.Add(field);
                }
            }

            // Get properties with both getter and setter
            foreach (var property in GetProperties(t))
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    var attribute = property.GetCustomAttribute<CloneableAttribute>();
                    if (attribute == null || attribute.IsCloneable)
                    {
                        members.Add(property);
                    }
                }
            }

            // Sort by priority if specified
            members = members
                .OrderByDescending(m =>
                {
                    var attr = m.GetCustomAttribute<CloneableAttribute>();
                    return attr?.Priority ?? 0;
                })
                .ToList();

            return new ReadOnlyCollection<MemberInfo>(members);
        });
    }

    /// <summary>
    ///     Determines if the specified type has a parameterless constructor.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type has a parameterless constructor; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasParameterlessConstructor(Type type)
    {
        return HasParameterlessConstructorCache.GetOrAdd(type, t =>
        {
            if (t.IsValueType)
            {
                return true; // Value types always have a parameterless constructor
            }

            return t.GetConstructor(Type.EmptyTypes) != null;
        });
    }

    /// <summary>
    ///     Gets the default value for the specified type.
    /// </summary>
    /// <param name="type">The type to get the default value for.</param>
    /// <returns>The default value for the specified type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    /// <summary>
    ///     Creates an instance of the specified type using the most efficient method available.
    /// </summary>
    /// <param name="type">The type to create an instance of.</param>
    /// <returns>A new instance of the specified type.</returns>
    /// <exception cref="MissingMethodException">Thrown when the type does not have a parameterless constructor.</exception>
    public static object CreateInstance(Type type)
    {
        if (!HasParameterlessConstructor(type))
        {
            throw new MissingMethodException($"Type {type.FullName} does not have a parameterless constructor.");
        }

        try
        {
            // Try to create the instance using Activator
            return Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating instance of type {Type}", type.Name);
            throw;
        }
    }

    /// <summary>
    ///     Clears all reflection caches.
    /// </summary>
    /// <remarks>
    ///     This can be useful in scenarios where types might be unloaded or modified dynamically.
    /// </remarks>
    public static void ClearCaches()
    {
        FieldsCache.Clear();
        PropertiesCache.Clear();
        ConstructorsCache.Clear();
        MethodsCache.Clear();
        CloneableMembersCache.Clear();
        HasParameterlessConstructorCache.Clear();

        Logger.Information("All reflection caches have been cleared");
    }
}
