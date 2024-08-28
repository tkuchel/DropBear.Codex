#region

using System.Diagnostics;
using DropBear.Codex.Core;
using Newtonsoft.Json;

#endregion

namespace DropBear.Codex.Utilities.DeepCloning;

/// <summary>
///     Provides extension methods for deep cloning objects using the <see cref="DeepCloner" />.
/// </summary>
public static class DeepClonerExtensions
{
    /// <summary>
    ///     Deep clones the specified object using the provided JSON serializer settings.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="obj">The object to clone.</param>
    /// <param name="settings">Optional JSON serializer settings to customize the cloning process.</param>
    /// <returns>A <see cref="Result{T}" /> containing the cloned object or a failure message if cloning fails.</returns>
    public static Result<T> Clone<T>(this T obj, JsonSerializerSettings? settings = null) where T : class
    {
        try
        {
            return DeepCloner.Clone(obj, settings);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error cloning object: {ex.Message}");
            return Result<T>.Failure("An error occurred while cloning the object: " + ex.Message);
        }
    }

    /// <summary>
    ///     Asynchronously deep clones the specified object using the provided JSON serializer settings.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="obj">The object to clone.</param>
    /// <param name="settings">Optional JSON serializer settings to customize the cloning process.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation, containing a <see cref="Result{T}" /> with the cloned
    ///     object or a failure message if cloning fails.
    /// </returns>
    public static async Task<Result<T>> CloneAsync<T>(this T obj, JsonSerializerSettings? settings = null)
        where T : class
    {
        try
        {
            return await Task.Run(() => obj.Clone(settings)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Async error cloning object: {ex.Message}");
            return Result<T>.Failure("An error occurred while asynchronously cloning the object: " + ex.Message);
        }
    }
}
