#region

using System.Reflection;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using Newtonsoft.Json;
using Serilog;
using ReferenceEqualityComparer = DropBear.Codex.Utilities.DeepCloning.Comparers.ReferenceEqualityComparer;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides functionality for deep cloning objects using both expression-based and JSON-based methods.
/// </summary>
public static class DeepCloner
{
    private static readonly JsonSerializerSettings DefaultSettings = new()
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        PreserveReferencesHandling = PreserveReferencesHandling.Objects
    };

    private static readonly Dictionary<Type, Delegate> ClonerCache = new();
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(DeepCloner));

    /// <summary>
    ///     Deep clones an object of type <typeparamref name="T" /> using the appropriate cloning method.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="source">The object to clone.</param>
    /// <param name="settings">Optional JSON serializer settings for JSON-based cloning.</param>
    /// <returns>A <see cref="Result{T}" /> containing the cloned object or an error message.</returns>
    public static Result<T> Clone<T>(T source, JsonSerializerSettings? settings = null) where T : class
    {
        if (source == null)
        {
            return Result<T>.Failure("Source object cannot be null.");
        }

        if (UseExpressionBasedCloning(typeof(T)))
        {
            try
            {
                var cloner = GetOrCreateCloner<T>();
                var track = new Dictionary<object, object>(
                    new ReferenceEqualityComparer()); // Create a tracking dictionary
                var clonedObject = cloner(source, track);
                return Result<T>.Success(clonedObject);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error cloning object with expression-based cloner.");
                return Result<T>.Failure("An error occurred while cloning the object: " + ex.Message);
            }
        }

        // Fallback to JSON-based cloning
        try
        {
            var jsonSettings = settings ?? DefaultSettings;
            var json = JsonConvert.SerializeObject(source, jsonSettings);
            var clonedObject = JsonConvert.DeserializeObject<T>(json, jsonSettings);
            if (clonedObject is null)
            {
                throw new InvalidOperationException("Cloning resulted in a null object.");
            }

            return Result<T>.Success(clonedObject);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error cloning object with JSON.");
            return Result<T>.Failure("An error occurred while cloning the object: " + ex.Message);
        }
    }

    /// <summary>
    ///     Determines whether to use expression-based cloning based on the type and attributes of the object.
    /// </summary>
    /// <param name="type">The type of the object.</param>
    /// <returns><c>true</c> if expression-based cloning should be used; otherwise, <c>false</c>.</returns>
    private static bool UseExpressionBasedCloning(Type type)
    {
        var attribute = type.GetCustomAttribute<CloneMethodAttribute>();
        if (attribute is not null)
        {
            return attribute.UseExpression;
        }

        return IsImmutable(type) || IsSimpleType(type);
    }

    /// <summary>
    ///     Checks if the type is considered immutable.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns><c>true</c> if the type is immutable; otherwise, <c>false</c>.</returns>
    private static bool IsImmutable(Type type)
    {
        return type.IsPrimitive || type == typeof(string) ||
               type.GetProperties().All(prop => prop.GetSetMethod() == null);
    }

    /// <summary>
    ///     Checks if the type is considered simple (i.e., has a limited number of fields and properties).
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns><c>true</c> if the type is simple; otherwise, <c>false</c>.</returns>
    private static bool IsSimpleType(Type type)
    {
        return type.GetProperties().Length <= 10 && type.GetFields().Length <= 10;
    }

    /// <summary>
    ///     Gets or creates a cloner function for the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <returns>A cloner function.</returns>
    private static Func<T, Dictionary<object, object>, T> GetOrCreateCloner<T>() where T : class
    {
        if (!ClonerCache.TryGetValue(typeof(T), out var cloner))
        {
            cloner = ExpressionCloner.GetCloner<T>();
            ClonerCache[typeof(T)] = cloner;
        }

        return (Func<T, Dictionary<object, object>, T>)cloner;
    }
}
