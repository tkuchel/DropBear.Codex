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
///     Optimized for .NET 9.
/// </summary>
public sealed class MessagePackEnvelopeSerializer : IEnvelopeSerializer
{
    private readonly MessagePackSerializerOptions _options;
    private readonly IResultTelemetry _telemetry;

    /// <summary>
    ///     Initializes a new instance with optional custom options.
    /// </summary>
    public MessagePackEnvelopeSerializer(
        MessagePackSerializerOptions? options = null,
        IResultTelemetry? telemetry = null)
    {
        _telemetry = telemetry ?? TelemetryProvider.Current;
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
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), nameof(Serialize));
            throw new InvalidOperationException("Failed to serialize envelope.", ex);
        }
    }

    /// <inheritdoc />
    public Envelope<T> Deserialize<T>(string data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);

        try
        {
            var dto = JsonSerializer.Deserialize<EnvelopeDto<T>>(data);
            if (dto is null)
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
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), nameof(Deserialize));
            throw new InvalidOperationException("Failed to deserialize envelope.", ex);
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
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), nameof(SerializeToBinary));
            throw new MessagePackSerializationException("Failed to serialize envelope to binary.", ex);
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
            var dto = MessagePackSerializer.Deserialize<EnvelopeDto<T>>(data, _options);
            if (dto is null)
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
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), nameof(DeserializeFromBinary));
            throw new MessagePackSerializationException("Failed to deserialize envelope from binary.", ex);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Creates an envelope from a DTO with validation.
    /// </summary>
    private Envelope<T> CreateEnvelopeFromDto<T>(EnvelopeDto<T> dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Validate DTO
        ValidateDto(dto);

        // Create envelope using FromDto method
        return Envelope<T>.FromDto(dto, _telemetry);
    }

    /// <summary>
    ///     Validates an envelope DTO before deserialization.
    /// </summary>
    private static void ValidateDto<T>(EnvelopeDto<T> dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Validate sealed envelopes have signatures
        if (dto.IsSealed && string.IsNullOrWhiteSpace(dto.Signature))
        {
            throw new MessagePackSerializationException("Sealed envelope must have a signature.");
        }

        // Validate sealed envelopes have sealed date
        if (dto is { IsSealed: true, SealedAt: null })
        {
            throw new MessagePackSerializationException("Sealed envelope must have a sealed date.");
        }

        // Validate created date exists
        if (dto.CreatedAt == default)
        {
            throw new MessagePackSerializationException("Envelope must have a creation date.");
        }
    }

    #endregion
}
