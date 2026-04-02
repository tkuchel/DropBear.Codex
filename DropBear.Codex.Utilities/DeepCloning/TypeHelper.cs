#region

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides utility methods for type inspection and analysis.
/// </summary>
internal static class TypeHelper
{
    // Cache for type immutability checks
    private static readonly ConcurrentDictionary<Type, bool> ImmutableTypesCache = new();

    /// <summary>
    ///     Determines if a type is immutable by checking if all its properties are read-only.
    ///     Uses strict checking that includes non-public setters.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is considered immutable; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsImmutableType(Type type)
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

            // Consider a type immutable if all properties have only getters (including non-public setters)
            // Use GetSetMethod(true) to check for non-public setters as well
            var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            return properties.Length > 0 && properties.All(p => p.GetSetMethod(true) == null);
        });
    }

    /// <summary>
    ///     Clears the immutability type cache.
    /// </summary>
    public static void ClearCache()
    {
        ImmutableTypesCache.Clear();
    }
}
