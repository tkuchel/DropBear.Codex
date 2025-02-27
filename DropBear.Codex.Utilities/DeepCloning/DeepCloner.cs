#region

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.DeepCloning.Attributes;
using DropBear.Codex.Utilities.Errors;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides functionality for deep cloning objects using both expression-based and JSON-based methods.
///     Optimized for performance and memory efficiency.
/// </summary>
public static class DeepCloner
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(DeepCloner));

    // Default JSON serialization settings with circular reference handling
    private static readonly JsonSerializerSettings DefaultSettings = new()
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore
    };

    // Cache compiled cloner expressions for better performance
    private static readonly ConcurrentDictionary<Type, Delegate> ClonerCache = new();

    // Track immutable types to skip unnecessary cloning
    private static readonly ConcurrentDictionary<Type, bool> ImmutableTypeCache = new();

    /// <summary>
    ///     Deep clones an object of type <typeparamref name="T" /> using the appropriate cloning method.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="source">The object to clone.</param>
    /// <param name="settings">Optional JSON serializer settings for JSON-based cloning.</param>
    /// <returns>A Result containing the cloned object or error information.</returns>
    public static Result<T, DeepCloneError> Clone<T>(T? source, JsonSerializerSettings? settings = null) where T : class
    {
        if (source == null)
        {
            return Result<T, DeepCloneError>.Success(null!);
        }

        // Fast path for immutable types - return the original
        if (IsImmutableType(typeof(T)))
        {
            Logger.Debug("Skipping clone for immutable type {Type}", typeof(T).Name);
            return Result<T, DeepCloneError>.Success(source);
        }

        // Determine cloning method
        var useExpressionCloning = ShouldUseExpressionBasedCloning(typeof(T));
        Logger.Debug("Using {CloneMethod} for type {Type}",
            useExpressionCloning ? "expression-based cloning" : "JSON-based cloning",
            typeof(T).Name);

        if (useExpressionCloning)
        {
            return CloneWithExpression(source);
        }

        return CloneWithJson(source, settings);
    }

    /// <summary>
    ///     Fast clones an object using shallow cloning techniques.
    ///     Suitable for simple objects with no nested complex types.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="source">The object to clone.</param>
    /// <returns>A Result containing the cloned object or error information.</returns>
    public static Result<T, DeepCloneError> FastClone<T>(T? source) where T : class
    {
        if (source == null)
        {
            return Result<T, DeepCloneError>.Success(null!);
        }

        if (IsImmutableType(typeof(T)))
        {
            return Result<T, DeepCloneError>.Success(source);
        }

        try
        {
            // Try to use MemberwiseClone via reflection for better performance
            var memberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (memberwiseCloneMethod != null)
            {
                var clone = (T)memberwiseCloneMethod.Invoke(source, null)!;
                return Result<T, DeepCloneError>.Success(clone);
            }

            // Fallback to manual property copying
            var instance = Activator.CreateInstance<T>();
            CopyProperties(source, instance);

            return Result<T, DeepCloneError>.Success(instance);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during fast clone of {Type}", typeof(T).Name);
            return Result<T, DeepCloneError>.Failure(
                new DeepCloneError($"Failed to fast clone object: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Asynchronously deep clones an object of type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="source">The object to clone.</param>
    /// <param name="settings">Optional JSON serializer settings.</param>
    /// <returns>A Task containing a Result with the cloned object or error information.</returns>
    public static Task<Result<T, DeepCloneError>> CloneAsync<T>(
        T? source,
        JsonSerializerSettings? settings = null)
        where T : class
    {
        return Task.Run(() => Clone(source, settings));
    }

    /// <summary>
    ///     Clones an object using expression-based cloning.
    /// </summary>
    private static Result<T, DeepCloneError> CloneWithExpression<T>(T source) where T : class
    {
        try
        {
            var cloner = GetOrCreateCloner<T>();
            var track = new Dictionary<object, object>(new Comparers.ReferenceEqualityComparer());
            var clonedObject = cloner(source, track);
            return Result<T, DeepCloneError>.Success(clonedObject);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error cloning object with expression-based cloner");
            return Result<T, DeepCloneError>.Failure(
                new DeepCloneError($"Failed to clone object using expressions: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Clones an object using JSON serialization.
    /// </summary>
    private static Result<T, DeepCloneError> CloneWithJson<T>(T source, JsonSerializerSettings? settings)
        where T : class
    {
        try
        {
            var jsonSettings = settings ?? DefaultSettings;
            var json = JsonConvert.SerializeObject(source, jsonSettings);
            var clonedObject = JsonConvert.DeserializeObject<T>(json, jsonSettings);

            if (clonedObject == null)
            {
                throw new InvalidOperationException("JSON deserialization resulted in a null object");
            }

            return Result<T, DeepCloneError>.Success(clonedObject);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error cloning object with JSON");
            return Result<T, DeepCloneError>.Failure(
                new DeepCloneError($"Failed to clone object using JSON: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Determines whether to use expression-based cloning based on the type and attributes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldUseExpressionBasedCloning(Type type)
    {
        // Check for explicit directive via attribute
        var attribute = type.GetCustomAttribute<CloneMethodAttribute>();
        if (attribute != null)
        {
            return attribute.UseExpression;
        }

        // Immutable types can use any method (they'll be short-circuited anyway)
        if (IsImmutableType(type))
        {
            return true;
        }

        // System primitive types and common types
        if (type.IsPrimitive || type == typeof(string) || type == typeof(Guid) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan))
        {
            return true;
        }

        // Simple objects are better with expression-based cloning
        var isSimple = IsSimpleType(type);

        // Use expressions for types with limited complexity
        return isSimple;
    }

    /// <summary>
    ///     Checks if the type is considered immutable.
    /// </summary>
    private static bool IsImmutableType(Type type)
    {
        return ImmutableTypeCache.GetOrAdd(type, t =>
        {
            // Primitive types, strings, and common immutable types
            if (t.IsPrimitive || t == typeof(string) || t == typeof(Guid) ||
                t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(TimeSpan) ||
                t == typeof(decimal) || t == typeof(Type) || t.IsEnum)
            {
                return true;
            }

            // System immutable collections
            if (t.Namespace?.StartsWith("System.Collections.Immutable", StringComparison.Ordinal) == true)
            {
                return true;
            }

            // Look for readonly properties only (no setters)
            var publicProperties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            return publicProperties.Length > 0 && publicProperties.All(p => p.GetSetMethod() == null);
        });
    }

    /// <summary>
    ///     Checks if the type is considered simple (i.e., has a limited number of fields and properties).
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

        // Check property count and complexity
        var fewProperties = properties.Length <= 20; // Increased threshold from 10
        var fewFields = fields.Length <= 10;

        // Check for presence of complex type properties
        var hasComplexProperties = properties.Any(p => !IsSimplePropertyType(p.PropertyType));

        return fewProperties && fewFields && !hasComplexProperties;
    }

    /// <summary>
    ///     Determines if a property type is simple enough for expression-based cloning.
    /// </summary>
    private static bool IsSimplePropertyType(Type propertyType)
    {
        // Check for primitive types, common types, and nullable variants
        if (propertyType.IsPrimitive || propertyType == typeof(string) ||
            propertyType == typeof(Guid) || propertyType == typeof(DateTime) ||
            propertyType == typeof(TimeSpan) || propertyType == typeof(decimal) ||
            propertyType == typeof(byte[]) || propertyType.IsEnum ||
            Nullable.GetUnderlyingType(propertyType) != null)
        {
            return true;
        }

        // Collections might be complex depending on element type
        if (propertyType.IsGenericType)
        {
            var genericTypeDefinition = propertyType.GetGenericTypeDefinition();

            // Check for common collections and dictionaries
            var isCollection = genericTypeDefinition == typeof(List<>) ||
                               genericTypeDefinition == typeof(HashSet<>) ||
                               genericTypeDefinition == typeof(Dictionary<,>);

            if (isCollection)
            {
                // Check element types
                foreach (var argType in propertyType.GetGenericArguments())
                {
                    if (!IsSimplePropertyType(argType))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        // Consider arrays of primitive types as simple
        if (propertyType.IsArray)
        {
            var elementType = propertyType.GetElementType();
            return elementType != null && IsSimplePropertyType(elementType);
        }

        // Non-primitive, non-collection type - likely complex
        return false;
    }

    /// <summary>
    ///     Gets or creates a cloner function for the specified type.
    /// </summary>
    private static Func<T, Dictionary<object, object>, T> GetOrCreateCloner<T>() where T : class
    {
        return (Func<T, Dictionary<object, object>, T>)ClonerCache.GetOrAdd(typeof(T), type =>
        {
            Logger.Debug("Creating expression-based cloner for type {Type}", type.Name);
            return ExpressionCloner.GetCloner<T>();
        });
    }

    /// <summary>
    ///     Copies all readable and writable properties from source to target.
    /// </summary>
    private static void CopyProperties<T>(T source, T target) where T : class
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(source);
                prop.SetValue(target, value);
            }
            catch
            {
                // Skip properties that throw during get/set
            }
        }
    }
}
