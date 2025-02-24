#region

using System.Text.Json;
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
    private readonly IResultTelemetry? _telemetry;

    // Optional constructor to inject telemetry
    public MessagePackEnvelopeSerializer(IResultTelemetry? telemetry = null)
    {
        _telemetry = telemetry ?? new DefaultResultTelemetry();
    }

    /// <inheritdoc />
    public string Serialize<T>(Envelope<T> envelope)
    {
        // Convert to DTO and serialize to JSON for string representation
        var dto = envelope.GetDto();
        return JsonSerializer.Serialize(dto);
    }

    /// <inheritdoc />
    public Envelope<T> Deserialize<T>(string data)
    {
        var dto = JsonSerializer.Deserialize<Envelope<T>.EnvelopeDto<T>>(data);

        if (dto == null)
        {
            throw new MessagePackSerializationException("Failed to deserialize envelope");
        }

        // Construct new envelope using the DTO constructor
        var envelope = new Envelope<T>(dto, _telemetry);
        return envelope;
    }

    /// <inheritdoc />
    public byte[] SerializeToBinary<T>(Envelope<T> envelope)
    {
        // Use MessagePack for efficient binary serialization
        var dto = envelope.GetDto();
        return MessagePackSerializer.Serialize(dto);
    }

    /// <inheritdoc />
    public Envelope<T> DeserializeFromBinary<T>(byte[] data)
    {
        var dto = MessagePackSerializer.Deserialize<Envelope<T>.EnvelopeDto<T>>(data);

        // Construct new envelope using the DTO constructor
        var envelope = new Envelope<T>(dto, _telemetry);
        return envelope;
    }
}
