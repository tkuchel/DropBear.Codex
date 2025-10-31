#region

using System.Runtime.CompilerServices;
using System.Text.Json;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;
using SystemJsonSerializer = System.Text.Json.JsonSerializer;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Provides streaming deserialization of JSON arrays using System.Text.Json,
///     allowing element-by-element processing without loading entire arrays into memory.
/// </summary>
public sealed class JsonStreamingDeserializer : IStreamingSerializer
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonStreamingDeserializer" /> class.
    /// </summary>
    /// <param name="options">JSON serializer options to use for deserialization</param>
    /// <param name="logger">Logger for diagnostics and error tracking</param>
    public JsonStreamingDeserializer(JsonSerializerOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        _logger = logger ?? Log.Logger;
    }

    /// <summary>
    ///     Deserializes a JSON array from a stream as an async enumerable,
    ///     yielding elements one at a time without loading the entire array into memory.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array</typeparam>
    /// <param name="stream">The stream containing the JSON array</param>
    /// <param name="cancellationToken">Cancellation token to stop deserialization</param>
    /// <returns>An async enumerable that yields Result-wrapped deserialized elements</returns>
    public async IAsyncEnumerable<Result<T, SerializationError>> DeserializeAsyncEnumerable<T>(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (stream is null)
        {
            _logger.Warning("Attempted to deserialize from null stream");
            yield return Result<T, SerializationError>.Failure(
                SerializationError.InvalidInput("Stream cannot be null"));
            yield break;
        }

        if (!stream.CanRead)
        {
            _logger.Warning("Attempted to deserialize from non-readable stream");
            yield return Result<T, SerializationError>.Failure(
                SerializationError.InvalidInput("Stream must be readable"));
            yield break;
        }

        _logger.Debug("Starting streaming deserialization for type {Type}", typeof(T).Name);

        var elementCount = 0;

        // Use System.Text.Json's built-in streaming deserialization
        var asyncEnumerable = SystemJsonSerializer.DeserializeAsyncEnumerable<T>(
            stream,
            _options,
            cancellationToken);

        if (asyncEnumerable is null)
        {
            _logger.Error("DeserializeAsyncEnumerable returned null for type {Type}", typeof(T).Name);
            yield return Result<T, SerializationError>.Failure(
                SerializationError.ForType<T>("Deserialization returned null enumerable", "StreamDeserialize"));
            yield break;
        }

        // Stream elements one by one
        await foreach (var element in asyncEnumerable.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            elementCount++;

            if (element is null)
            {
                _logger.Warning("Encountered null element at index {Index} during streaming deserialization",
                    elementCount - 1);

                if (default(T) is null)
                {
                    // Null is valid for nullable types
                    yield return Result<T, SerializationError>.Success(element!);
                }
                else
                {
                    // Null is invalid for non-nullable types
                    yield return Result<T, SerializationError>.Failure(
                        SerializationError.ForType<T>($"Null element encountered at index {elementCount - 1}",
                            "StreamDeserialize"));
                }
            }
            else
            {
                yield return Result<T, SerializationError>.Success(element);
            }
        }

        _logger.Debug("Completed streaming deserialization for type {Type}. Total elements: {Count}", typeof(T).Name,
            elementCount);
    }

    /// <summary>
    ///     Determines whether this serializer can handle streaming deserialization for the specified type.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the serializer can handle streaming deserialization for the type; otherwise, false</returns>
    public bool CanStreamType(Type type)
    {
        if (type is null)
        {
            return false;
        }

        // System.Text.Json can handle most types, but some are problematic for streaming
        // Exclude types that don't make sense in a streaming context
        if (type == typeof(object))
        {
            return false; // Too ambiguous
        }

        // Check if the type is serializable by System.Text.Json
        try
        {
            // Most types are streamable with System.Text.Json
            // We'll rely on runtime errors for truly problematic types
            return true;
        }
        catch
        {
            return false;
        }
    }
}
