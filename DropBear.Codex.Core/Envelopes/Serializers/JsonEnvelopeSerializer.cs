#region

using System.Collections.Frozen;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Diagnostics;

#endregion

namespace DropBear.Codex.Core.Envelopes.Serializers;

/// <summary>
///     Provides JSON-based serialization for envelopes using System.Text.Json.
///     Optimized for .NET 9 with source generators.
/// </summary>
public sealed class JsonEnvelopeSerializer : IEnvelopeSerializer
{
    private readonly IResultTelemetry _telemetry;
    private readonly JsonSerializerOptions _options;

    /// <summary>
    ///     Initializes a new instance with optional custom options.
    /// </summary>
    public JsonEnvelopeSerializer(
        JsonSerializerOptions? options = null,
        IResultTelemetry? telemetry = null)
    {
        _telemetry = telemetry ?? new DefaultResultTelemetry();
        _options = options ?? CreateDefaultOptions();
    }

    /// <summary>
    ///     Creates default JSON serialization options optimized for performance.
    /// </summary>
    private static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = false, // Better performance
            PropertyNameCaseInsensitive = false, // Better performance
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

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
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "JsonSerialize");
            throw new InvalidOperationException("Failed to serialize envelope to JSON.", ex);
        }
    }

    /// <inheritdoc />
    public Envelope<T> Deserialize<T>(string data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(data);

        try
        {
            var dto = JsonSerializer.Deserialize<Envelope<T>.EnvelopeDto<T>>(data, _options);
            if (dto == null)
            {
                throw new JsonException("JSON deserialization returned null.");
            }

            return CreateEnvelopeFromDto(dto);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "JsonDeserialize");
            throw;
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "JsonDeserialize");
            throw new JsonException("Failed to deserialize the envelope from JSON.", ex);
        }
    }

    /// <inheritdoc />
    public byte[] SerializeToBinary<T>(Envelope<T> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        try
        {
            var serializedString = Serialize(envelope);
            return Encoding.UTF8.GetBytes(serializedString);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "JsonSerializeToBinary");
            throw;
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
            var serializedString = Encoding.UTF8.GetString(data);
            return Deserialize<T>(serializedString);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(T), "JsonDeserializeFromBinary");
            throw;
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

/// <summary>
///     Source-generated JSON context for better performance.
///     This will be used when source generators are enabled.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Envelope<>.EnvelopeDto<>))]
internal partial class EnvelopeJsonContext : JsonSerializerContext
{
}
