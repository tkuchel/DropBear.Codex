#region

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;
using Newtonsoft.Json;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides extension methods for deep cloning objects using the <see cref="DeepCloner" />.
/// </summary>
public static class DeepClonerExtensions
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(DeepClonerExtensions));

    /// <summary>
    ///     Deep clones the specified object using the provided JSON serializer settings.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="obj">The object to clone.</param>
    /// <param name="settings">Optional JSON serializer settings to customize the cloning process.</param>
    /// <returns>A Result containing the cloned object or error information.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, DeepCloneError> Clone<T>(this T obj, JsonSerializerSettings? settings = null)
        where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        var result = DeepCloner.Clone(obj, settings);
        stopwatch.Stop();

        if (result.IsSuccess)
        {
            Logger.Debug("Object of type {Type} cloned successfully in {ElapsedMilliseconds}ms",
                typeof(T).Name, stopwatch.ElapsedMilliseconds);
        }

        return result;
    }

    /// <summary>
    ///     Asynchronously deep clones the specified object using the provided JSON serializer settings.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="obj">The object to clone.</param>
    /// <param name="settings">Optional JSON serializer settings to customize the cloning process.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a Result with the cloned
    ///     object or error information.
    /// </returns>
    public static async Task<Result<T, DeepCloneError>> CloneAsync<T>(
        this T obj,
        JsonSerializerSettings? settings = null)
        where T : class
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await DeepCloner.CloneAsync(obj, settings).ConfigureAwait(false);
            stopwatch.Stop();

            if (result.IsSuccess)
            {
                Logger.Debug("Object of type {Type} cloned asynchronously in {ElapsedMilliseconds}ms",
                    typeof(T).Name, stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Error(ex, "Async error cloning object of type {Type} after {ElapsedMilliseconds}ms",
                typeof(T).Name, stopwatch.ElapsedMilliseconds);

            return Result<T, DeepCloneError>.Failure(
                new DeepCloneError($"An error occurred during asynchronous cloning: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Performs a fast shallow clone of the object.
    ///     Suitable for simple objects with no nested complex types.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="obj">The object to clone.</param>
    /// <returns>A Result containing the cloned object or error information.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, DeepCloneError> FastClone<T>(this T obj) where T : class
    {
        return DeepCloner.FastClone(obj);
    }

    /// <summary>
    ///     Clones an object and then applies a transformation to the cloned object.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="obj">The object to clone.</param>
    /// <param name="transform">A function to transform the cloned object.</param>
    /// <returns>A Result containing the transformed clone or error information.</returns>
    public static Result<T, DeepCloneError> CloneAndTransform<T>(
        this T obj,
        Action<T> transform)
        where T : class
    {
        if (obj == null)
        {
            return Result<T, DeepCloneError>.Success(null!);
        }

        var result = DeepCloner.Clone(obj);

        if (!result.IsSuccess)
        {
            return result;
        }

        try
        {
            transform(result.Value!);
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error transforming cloned object of type {Type}", typeof(T).Name);
            return Result<T, DeepCloneError>.Failure(
                new DeepCloneError($"Error transforming cloned object: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Clones an object of one type and converts it to another type.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="obj">The object to clone.</param>
    /// <param name="converter">A function to convert from source to destination type.</param>
    /// <returns>A Result containing the converted clone or error information.</returns>
    public static Result<TDestination, DeepCloneError> CloneAs<TSource, TDestination>(
        this TSource obj,
        Func<TSource, TDestination> converter)
        where TSource : class
        where TDestination : class
    {
        if (obj == null)
        {
            return Result<TDestination, DeepCloneError>.Success(null!);
        }

        try
        {
            var cloneResult = DeepCloner.Clone(obj);

            if (!cloneResult.IsSuccess)
            {
                return Result<TDestination, DeepCloneError>.Failure(cloneResult.Error!);
            }

            var converted = converter(cloneResult.Value!);
            return Result<TDestination, DeepCloneError>.Success(converted);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error cloning and converting object from {SourceType} to {DestinationType}",
                typeof(TSource).Name, typeof(TDestination).Name);

            return Result<TDestination, DeepCloneError>.Failure(
                new DeepCloneError($"Error converting cloned object: {ex.Message}"), ex);
        }
    }
}
