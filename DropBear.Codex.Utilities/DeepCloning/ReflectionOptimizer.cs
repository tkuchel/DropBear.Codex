#region

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reflection;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides optimized access to reflection-based information such as fields and properties using caching mechanisms.
/// </summary>
public static class ReflectionOptimizer
{
    private static readonly ConcurrentDictionary<Type, Collection<FieldInfo>> FieldsCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesCache = new();

    /// <summary>
    ///     Retrieves the fields of the specified type, utilizing caching for performance.
    /// </summary>
    /// <param name="type">The type whose fields are to be retrieved.</param>
    /// <returns>A collection of <see cref="FieldInfo" /> objects representing the fields of the specified type.</returns>
    public static Collection<FieldInfo> GetFields(Type type)
    {
        return FieldsCache.GetOrAdd(type, t =>
        {
            var fields = new Collection<FieldInfo>();
            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                fields.Add(field);
            }

            return fields;
        });
    }

    /// <summary>
    ///     Retrieves the properties of the specified type, utilizing caching for performance.
    /// </summary>
    /// <param name="type">The type whose properties are to be retrieved.</param>
    /// <returns>An array of <see cref="PropertyInfo" /> objects representing the properties of the specified type.</returns>
    public static PropertyInfo[] GetProperties(Type type)
    {
        return PropertiesCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        );
    }
}
