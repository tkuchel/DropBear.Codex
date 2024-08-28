#region

using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Exceptions;
using DropBear.Codex.Serialization.Interfaces;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer implementation for JSON serialization and deserialization.
/// </summary>
public class JsonSerializer : ISerializer
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<JsonSerializer>();
    private readonly RecyclableMemoryStreamManager _memoryManager;
    private readonly JsonSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonSerializer" /> class.
    /// </summary>
    public JsonSerializer(SerializationConfig config)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config), "Configuration must be provided.");
        }

        _options = config.JsonSerializerOptions ?? new JsonSerializerOptions();
        _memoryManager = config.RecyclableMemoryStreamManager ?? throw new ArgumentNullException(
            nameof(config.RecyclableMemoryStreamManager), "RecyclableMemoryStreamManager must be provided.");
    }

    /// <inheritdoc />
    public async Task<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        _logger.Information("Starting JSON serialization for type {Type}.", typeof(T));

        try
        {
            var memoryStream = new RecyclableMemoryStream(_memoryManager);
            await using (memoryStream.ConfigureAwait(false))
            {
                await System.Text.Json.JsonSerializer.SerializeAsync(memoryStream, value, _options, cancellationToken)
                    .ConfigureAwait(false);
                _logger.Information("JSON serialization completed successfully for type {Type}.", typeof(T));
                return memoryStream.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during JSON serialization for type {Type}.", typeof(T));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
    {
        _logger.Information("Starting JSON deserialization for type {Type}.", typeof(T));

        try
        {
            var memoryStream = new RecyclableMemoryStream(_memoryManager);
            await using (memoryStream.ConfigureAwait(false))
            {
                await memoryStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                memoryStream.Seek(0, SeekOrigin.Begin);
                var result = await System.Text.Json.JsonSerializer
                    .DeserializeAsync<T>(memoryStream, _options, cancellationToken)
                    .ConfigureAwait(false) ?? throw new DeserializationException("Failed to deserialize data");

                _logger.Information("JSON deserialization completed successfully for type {Type}.", typeof(T));
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error occurred during JSON deserialization for type {Type}.", typeof(T));
            throw;
        }
    }
}
