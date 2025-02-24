#region

using System.Text.Json;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Extensions;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Diagnostics;
using MessagePack;

#endregion

namespace DropBear.Codex.Core.Envelopes.Serializers;

/// <summary>
///     Provides MessagePack-based serialization for high-performance binary envelope serialization.
/// </summary>
public sealed class MessagePackEnvelopeSerializer : IEnvelopeSerializer
{
    private readonly IResultTelemetry _telemetry;

    /// <summary>
    ///     Initializes an instance of <see cref="MessagePackEnvelopeSerializer" /> with optional telemetry.
    /// </summary>
    /// <param name="telemetry">Optional telemetry interface for recording serialization diagnostics.</param>
    public MessagePackEnvelopeSerializer(IResultTelemetry? telemetry = null)
    {
        _telemetry = telemetry ?? new DefaultResultTelemetry();
    }


    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="envelope" /> is null.</exception>
    /// <remarks>
    ///     For the <see cref="string" /> output, we simply serialize the DTO to JSON for convenience/human-readability.
    /// </remarks>
    public string Serialize<T>(Envelope<T> envelope)
    {
        if (envelope == null)
        {
            throw new ArgumentNullException(nameof(envelope), "Envelope cannot be null.");
        }

        try
        {
            var dto = envelope.GetDto();
            return JsonSerializer.Serialize(dto);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T));
            throw new InvalidOperationException("Failed to serialize envelope to JSON fallback.", ex);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown if <paramref name="data" /> is null or whitespace.</exception>
    /// <exception cref="MessagePackSerializationException">Thrown if the data cannot be deserialized.</exception>
    public Envelope<T> Deserialize<T>(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            throw new ArgumentException("Serialized data cannot be null or whitespace.", nameof(data));
        }

        try
        {
            var dto = JsonSerializer.Deserialize<Envelope<T>.EnvelopeDto<T>>(data);
            if (dto == null)
            {
                throw new MessagePackSerializationException("Deserialization returned null DTO.");
            }

            return new Envelope<T>(dto, _telemetry);
        }
        catch (MessagePackSerializationException)
        {
            // Already a specialized exception; just rethrow
            throw;
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T));
            throw new MessagePackSerializationException("Failed to deserialize envelope from JSON fallback.", ex);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="envelope" /> is null.</exception>
    public byte[] SerializeToBinary<T>(Envelope<T> envelope)
    {
        if (envelope == null)
        {
            throw new ArgumentNullException(nameof(envelope), "Envelope cannot be null.");
        }

        try
        {
            var dto = envelope.GetDto();
            var options = MessagePackConfig.GetOptions(); // Optional: use custom options

            return MessagePackSerializer.Serialize(dto, options);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T));
            throw new MessagePackSerializationException("Failed to serialize envelope to MessagePack.", ex);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown if <paramref name="data" /> is null or empty.</exception>
    /// <exception cref="MessagePackSerializationException">Thrown if the data cannot be deserialized.</exception>
    public Envelope<T> DeserializeFromBinary<T>(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Binary data cannot be null or empty.", nameof(data));
        }

        try
        {
            var options = MessagePackConfig.GetOptions(); // Optional: use custom options

            var dto = MessagePackSerializer.Deserialize<Envelope<T>.EnvelopeDto<T>>(data, options);
            return new Envelope<T>(dto, _telemetry);
        }
        catch (MessagePackSerializationException)
        {
            // Rethrow for the caller to handle if needed
            throw;
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T));
            throw new MessagePackSerializationException("Failed to deserialize envelope from MessagePack.", ex);
        }
    }
}
