#region

using System.Collections.Frozen;
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
///     Optimized for .NET 9.
/// </summary>
public sealed class MessagePackEnvelopeSerializer : IEnvelopeSerializer
{
    private readonly IResultTelemetry _telemetry;
    private readonly MessagePackSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance with optional custom options.
    /// </summary>
    public MessagePackEnvelopeSerializer(
        MessagePackSerializerOptions? options = null,
        IResultTelemetry? telemetry = null)
    {
        _telemetry = telemetry ?? new DefaultResultTelemetry();
        _options = options ?? MessagePackConfig.GetOptions();
    }

    #region IEnvelopeSerializer Implementation

    /// <inheritdoc />
    public string Serialize<T>(Envelope<T> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            // For string output, use JSON as MessagePack is binary
            var dto = envelope.GetDto();
            return JsonSerializer.Serialize(dto);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "MessagePackSerialize");
            throw new InvalidOperationException("Failed to serialize envelope.", ex);
        }
    }

    /// <inheritdoc />
    public Envelope<T> Deserialize<T>(string data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);

        try
        {
            var dto = JsonSerializer.Deserialize<Envelope<T>.EnvelopeDto<T>>(data);
            if (dto == null)
            {
                throw new MessagePackSerializationException("Deserialization returned null DTO.");
            }

            return CreateEnvelopeFromDto(dto);
        }
        catch (MessagePackSerializationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "MessagePackDeserialize");
            throw new MessagePackSerializationException("Failed to deserialize envelope.", ex);
        }
    }

    /// <inheritdoc />
    public byte[] SerializeToBinary<T>(Envelope<T> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            var dto = envelope.GetDto();
            return MessagePackSerializer.Serialize(dto, _options);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "MessagePackSerializeToBinary");
            throw new MessagePackSerializationException("Failed to serialize envelope to MessagePack.", ex);
        }
    }

    /// <inheritdoc />
    public Envelope<T> DeserializeFromBinary<T>(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length == 0)
        {
            throw new ArgumentException("Binary data cannot be empty.", nameof(data));
        }

        try
        {
            var dto = MessagePackSerializer.Deserialize<Envelope<T>.EnvelopeDto<T>>(data, _options);
            return CreateEnvelopeFromDto(dto);
        }
        catch (MessagePackSerializationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "MessagePackDeserializeFromBinary");
            throw new MessagePackSerializationException("Failed to deserialize envelope from MessagePack.", ex);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates an envelope from a deserialized DTO.
    /// </summary>
    private Envelope<T> CreateEnvelopeFromDto<T>(Envelope<T>.EnvelopeDto<T> dto)
    {
        var headers = dto.Headers?.ToFrozenDictionary(StringComparer.Ordinal)
                      ?? System.Collections.Frozen.FrozenDictionary<string, object>.Empty;

        byte[]? encryptedPayload = null;
        if (!string.IsNullOrEmpty(dto.EncryptedPayload))
        {
            encryptedPayload = Convert.FromBase64String(dto.EncryptedPayload);
        }

        return new Envelope<T>(
            dto.Payload,
            headers,
            dto.IsSealed,
            dto.CreatedDate,
            dto.SealedDate,
            dto.Signature,
            encryptedPayload,
            _telemetry);
    }

    #endregion
}
