#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;

#endregion

namespace DropBear.Codex.Serialization.Interfaces;

/// <summary>
///     Interface for serializers that support streaming element-by-element deserialization
///     of JSON arrays without loading the entire array into memory.
/// </summary>
public interface IStreamingSerializer
{
    /// <summary>
    ///     Deserializes a JSON array from a stream as an async enumerable,
    ///     yielding elements one at a time without loading the entire array into memory.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array</typeparam>
    /// <param name="stream">The stream containing the JSON array</param>
    /// <param name="cancellationToken">Cancellation token to stop deserialization</param>
    /// <returns>An async enumerable that yields deserialized elements</returns>
    /// <exception cref="InvalidOperationException">Thrown when streaming is not supported or stream is invalid</exception>
    IAsyncEnumerable<Result<T, SerializationError>> DeserializeAsyncEnumerable<T>(
        Stream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Determines whether this serializer can handle streaming deserialization for the specified type.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the serializer can handle streaming deserialization for the type; otherwise, false</returns>
    bool CanStreamType(Type type);
}
