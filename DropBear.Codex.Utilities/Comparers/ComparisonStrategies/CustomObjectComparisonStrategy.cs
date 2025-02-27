#region

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Utilities.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Comparers.ComparisonStrategies;

/// <summary>
///     A comparison strategy for custom object types that are not strings, not enumerations, and not IEnumerable.
///     Compares property-by-property using <see cref="ObjectComparer.CompareValues" />.
/// </summary>
public sealed class CustomObjectComparisonStrategy : IComparisonStrategy
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<CustomObjectComparisonStrategy>();

    // Cache for property lookups to improve performance
    private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new();
    private static readonly object PropertyCacheLock = new();

    /// <inheritdoc />
    public bool CanCompare(Type type)
    {
        // We exclude:
        // 1) Primitive or enum types
        // 2) Strings
        // 3) Anything that implements IEnumerable
        return !type.IsPrimitive &&
               !type.IsEnum &&
               type != typeof(string) &&
               !typeof(IEnumerable).IsAssignableFrom(type);
    }

    /// <inheritdoc />
    public double Compare(object? value1, object? value2, int currentDepth = 0, int maxDepth = 10)
    {
        if (value1 == null || value2 == null || value1.GetType() != value2.GetType())
        {
            return 0;
        }

        // Fast reference equality check
        if (ReferenceEquals(value1, value2))
        {
            return 1.0;
        }

        // Check recursion depth to prevent stack overflow
        if (currentDepth >= maxDepth)
        {
            Logger.Debug("Maximum recursion depth reached for type {Type}. Returning partial confidence.",
                value1.GetType().Name);
            return 0.5; // Return medium confidence to indicate potentially similar but unverified
        }

        var properties = GetCachedProperties(value1.GetType());

        if (properties.Length == 0)
        {
            // No comparable properties found
            return value1.Equals(value2) ? 1.0 : 0.0;
        }

        double totalScore = 0;
        var propertiesCompared = 0;

        // Compare each property
        foreach (var prop in properties)
        {
            try
            {
                var propValue1 = prop.GetValue(value1);
                var propValue2 = prop.GetValue(value2);

                var propertyScore = ObjectComparer.CompareValues(
                    propValue1, propValue2, currentDepth + 1, maxDepth);

                totalScore += propertyScore;
                propertiesCompared++;
            }
            catch (Exception ex)
            {
                // Log and skip problematic properties
                Logger.Warning(ex, "Error comparing property {Property} on type {Type}",
                    prop.Name, value1.GetType().Name);
            }
        }

        return propertiesCompared > 0
            ? totalScore / propertiesCompared
            : 0;
    }

    /// <summary>
    ///     Gets properties for the given type from cache or retrieves and caches them.
    /// </summary>
    /// <param name="type">The type to get properties for.</param>
    /// <returns>Array of PropertyInfo objects for the specified type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PropertyInfo[] GetCachedProperties(Type type)
    {
        // Check cache first for performance
        if (PropertyCache.TryGetValue(type, out var cachedProperties))
        {
            return cachedProperties;
        }

        // Cache miss, retrieve and cache properties
        lock (PropertyCacheLock)
        {
            // Double-check locking pattern
            if (PropertyCache.TryGetValue(type, out cachedProperties))
            {
                return cachedProperties;
            }

            // Get only readable public instance properties with no index parameters
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && !p.GetIndexParameters().Any())
                .ToArray();

            PropertyCache[type] = properties;
            return properties;
        }
    }
}
