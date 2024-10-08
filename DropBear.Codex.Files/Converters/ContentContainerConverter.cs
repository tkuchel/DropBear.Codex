﻿#region

using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Files.Enums;
using DropBear.Codex.Files.Models;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Converters;

/// <summary>
///     A custom JSON converter for serializing and deserializing <see cref="ContentContainer" /> objects.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ContentContainerConverter : JsonConverter<ContentContainer>
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentContainerConverter" /> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging errors and information.</param>
    public ContentContainerConverter(ILogger? logger = null)
    {
        _logger = logger ?? LoggerFactory.Logger.ForContext<ContentContainerConverter>();
    }

    /// <summary>
    ///     Reads and converts the JSON to a <see cref="ContentContainer" />.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The serializer options.</param>
    /// <returns>The <see cref="ContentContainer" /> represented by the JSON object.</returns>
    public override ContentContainer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var container = new ContentContainer();
        var providers = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    container.SetProviders(providers);
                    return container;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Unexpected token type: {reader.TokenType}");
                }

                var propertyName = reader.GetString();
                reader.Read(); // Move to the value token

                switch (propertyName?.ToLowerInvariant())
                {
                    case "flags":
                        container.EnableFlag((ContentContainerFlags)reader.GetInt32());
                        break;
                    case "contenttype":
                        container.SetContentType(reader.GetString() ?? string.Empty);
                        break;
                    case "data":
                        container.Data = reader.TokenType == JsonTokenType.Null
                            ? null
                            : JsonSerializer.Deserialize<byte[]>(ref reader, options);
                        break;
                    case "hash":
                        container.SetHash(reader.GetString());
                        break;
                    case "providers":
                        providers = JsonSerializer.Deserialize<Dictionary<string, Type>>(ref reader, options)
                                    ?? new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                        break;
                    default:
                        _logger.Warning("Unsupported property encountered: {PropertyName}", propertyName);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            var errorMessage = new StringBuilder("An error occurred while deserializing the ContentContainer: ");
            errorMessage.Append(ex.Message);
            _logger.Error(ex, errorMessage.ToString());
            throw new JsonException(errorMessage.ToString(), ex);
        }

        _logger.Error("EndObject token was expected but not found.");
        throw new JsonException("Expected EndObject token.");
    }

    /// <summary>
    ///     Writes the <see cref="ContentContainer" /> to JSON.
    /// </summary>
    /// <param name="writer">The UTF-8 JSON writer.</param>
    /// <param name="value">The <see cref="ContentContainer" /> to write.</param>
    /// <param name="options">The serializer options.</param>
    public override void Write(Utf8JsonWriter writer, ContentContainer value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber("flags", (int)value.Flags);
        writer.WriteString("contentType", value.ContentType);

        writer.WritePropertyName("data");
        JsonSerializer.Serialize(writer, value.Data, options);

        writer.WriteString("hash", value.Hash);

        writer.WritePropertyName("providers");
        JsonSerializer.Serialize(writer, value.GetProvidersDictionary(), options);

        writer.WriteEndObject();
    }
}
