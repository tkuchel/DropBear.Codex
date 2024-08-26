#region

using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     A serializer that handles both serialization and deserialization of JSON data using streams.
/// </summary>
public class JsonStreamSerializer : IStreamSerializer
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<JsonStreamSerializer>();
    private readonly JsonSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonStreamSerializer" /> class with the specified options.
    /// </summary>
    /// <param name="options">The JSON serializer options.</param>
    public JsonStreamSerializer(JsonSerializerOptions options)
    {
        _options = options ??
                   throw new ArgumentNullException(nameof(options), "JSON serializer options cannot be null.");
    }

    /// <summary>
    ///     Serializes an object to a JSON format and writes it to a provided stream.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="stream">The output stream to write the serialized data to.</param>
    /// <param name="value">The object to serialize.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            _logger.Error("The provided stream is null.");
            throw new ArgumentNullException(nameof(stream), "Stream cannot be null.");
        }

        _logger.Information("Starting JSON stream serialization for type {Type}.", typeof(T));

        try
        {
            await System.Text.Json.JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken)
                .ConfigureAwait(false);
            _logger.Information("JSON stream serialization completed successfully for type {Type}.", typeof(T));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during JSON stream serialization for type {Type}.", typeof(T));
            throw;
        }
    }

    /// <summary>
    ///     Deserializes JSON data from a stream into an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="stream">The input stream to read the JSON data from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation and returns the deserialized object.</returns>
    public async Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            _logger.Error("The provided stream is null.");
            throw new ArgumentNullException(nameof(stream), "Stream cannot be null.");
        }

        _logger.Information("Starting JSON stream deserialization for type {Type}.", typeof(T));

        try
        {
            var result = await System.Text.Json.JsonSerializer
                             .DeserializeAsync<T>(stream, _options, cancellationToken)
                             .ConfigureAwait(false) ??
                         throw new InvalidOperationException("Failed to deserialize JSON stream data.");

            _logger.Information("JSON stream deserialization completed successfully for type {Type}.", typeof(T));
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during JSON stream deserialization for type {Type}.", typeof(T));
            throw;
        }
    }
}
