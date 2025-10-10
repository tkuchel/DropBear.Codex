#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Envelopes.Serializers;

/// <summary>
///     Provides JSON-based serialization for envelopes.
///     Optimized for .NET 9 with modern JSON serialization.
/// </summary>
public sealed class JsonEnvelopeSerializer : IEnvelopeSerializer
{
    private readonly JsonSerializerOptions _options;
    private readonly IResultTelemetry _telemetry;

    /// <summary>
    ///     Initializes a new instance with optional custom options.
    /// </summary>
    public JsonEnvelopeSerializer(
        JsonSerializerOptions? options = null,
        IResultTelemetry? telemetry = null)
    {
        _telemetry = telemetry ?? TelemetryProvider.Current;
        _options = options ?? CreateDefaultOptions();
    }

    /// <summary>
    ///     Creates default JSON serializer options optimized for envelopes.
    /// </summary>
    private static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
    }

    #region Helper Methods

    /// <summary>
    ///     Validates an envelope DTO before deserialization.
    /// </summary>
    private static void ValidateDto<T>(EnvelopeDto<T> dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Validate sealed envelopes have signatures
        if (dto.IsSealed && string.IsNullOrWhiteSpace(dto.Signature))
        {
            throw new JsonException("Sealed envelope must have a signature.");
        }

        // Validate sealed envelopes have sealed date
        if (dto is { IsSealed: true, SealedAt: null })
        {
            throw new JsonException("Sealed envelope must have a sealed date.");
        }

        // Validate created date exists
        if (dto.CreatedAt == default)
        {
            throw new JsonException("Envelope must have a creation date.");
        }
    }

    #endregion

    #region IEnvelopeSerializer Implementation

    /// <inheritdoc />
    public string Serialize<T>(Envelope<T> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            var dto = envelope.GetDto();
            return JsonSerializer.Serialize(dto, _options);
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
            var dto = JsonSerializer.Deserialize<EnvelopeDto<T>>(data, _options);

            if (dto is null)
            {
                throw new JsonException("Deserialization returned null DTO.");
            }

            ValidateDto(dto);

            return Envelope<T>.FromDto(dto, _telemetry);
        }
        catch (JsonException)
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
            // Use UTF8 JSON for binary serialization
            var dto = envelope.GetDto();
            return JsonSerializer.SerializeToUtf8Bytes(dto, _options);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), nameof(SerializeToBinary));
            throw new InvalidOperationException("Failed to serialize envelope to binary.", ex);
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
            var dto = JsonSerializer.Deserialize<EnvelopeDto<T>>(data, _options);

            if (dto is null)
            {
                throw new JsonException("Deserialization returned null DTO.");
            }

            ValidateDto(dto);

            return Envelope<T>.FromDto(dto, _telemetry);
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), nameof(DeserializeFromBinary));
            throw new InvalidOperationException("Failed to deserialize envelope from binary.", ex);
        }
    }

    #endregion
}
