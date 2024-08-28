#region

using DropBear.Codex.Core.Logging;
using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Exceptions;
using DropBear.Codex.Serialization.Interfaces;
using MessagePack;
using Microsoft.IO;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Serializers;

/// <summary>
///     Serializer implementation for MessagePack serialization and deserialization.
/// </summary>
public class MessagePackSerializer : ISerializer
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<MessagePackSerializer>();
    private readonly RecyclableMemoryStreamManager _memoryManager;
    private readonly MessagePackSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MessagePackSerializer" /> class.
    /// </summary>
    public MessagePackSerializer(SerializationConfig config)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config), "Configuration must be provided.");
        }

        _options = config.MessagePackSerializerOptions ?? MessagePackSerializerOptions.Standard;
        _memoryManager = config.RecyclableMemoryStreamManager ?? throw new ArgumentNullException(
            nameof(config.RecyclableMemoryStreamManager), "RecyclableMemoryStreamManager must be provided.");
    }

    /// <inheritdoc />
    public async Task<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        _logger.Information("Starting MessagePack serialization for type {Type}.", typeof(T));

        try
        {
            var memoryStream = new RecyclableMemoryStream(_memoryManager);
            await using (memoryStream.ConfigureAwait(false))
            {
                await MessagePack.MessagePackSerializer
                    .SerializeAsync(memoryStream, value, _options, cancellationToken)
                    .ConfigureAwait(false);

                _logger.Information("MessagePack serialization completed successfully for type {Type}.", typeof(T));
                return memoryStream.ToArray();
            }
        }
        catch (MessagePackSerializationException ex) when (ex.InnerException is FormatterNotRegisteredException)
        {
            _logger.Error(ex, "Serialization error: Formatter not registered for type {Type}.", typeof(T));
            throw new SerializationException(
                "Error occurred while serializing data. Ensure all types are registered.", ex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "General serialization error occurred for type {Type}.", typeof(T));
            throw new SerializationException("Error occurred while serializing data.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<T> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
    {
        _logger.Information("Starting MessagePack deserialization for type {Type}.", typeof(T));

        try
        {
            var memoryStream = new RecyclableMemoryStream(_memoryManager);
            await using (memoryStream.ConfigureAwait(false))
            {
                await memoryStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                memoryStream.Seek(0, SeekOrigin.Begin);

                var result = await MessagePack.MessagePackSerializer
                    .DeserializeAsync<T>(memoryStream, _options, cancellationToken)
                    .ConfigureAwait(false);

                _logger.Information("MessagePack deserialization completed successfully for type {Type}.", typeof(T));
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "General deserialization error occurred for type {Type}.", typeof(T));
            throw new SerializationException("Error occurred while deserializing data.", ex);
        }
    }
}
