#region

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
    /// <param name="logger">
    ///     An optional <see cref="ILogger" /> instance for logging errors and information.
    ///     If not provided, a default logger is created.
    /// </param>
    public ContentContainerConverter(ILogger? logger = null)
    {
        _logger = logger ?? LoggerFactory.Logger.ForContext<ContentContainerConverter>();
    }

    /// <summary>
    ///     Reads and converts JSON into a <see cref="ContentContainer" />.
    /// </summary>
    /// <param name="reader">A <see cref="Utf8JsonReader" /> pointing to the JSON input.</param>
    /// <param name="typeToConvert">The type to convert (always <see cref="ContentContainer" />).</param>
    /// <param name="options">Serializer options, including any custom converters.</param>
    /// <returns>A new <see cref="ContentContainer" /> instance populated from JSON.</returns>
    /// <exception cref="JsonException">
    ///     Thrown when the JSON is invalid or lacks expected tokens/properties.
    /// </exception>
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
                    // Once we reach the end of this object, set the providers on the container
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
                        // Convert integer to enum flags
                        container.EnableFlag((ContentContainerFlags)reader.GetInt32());
                        break;

                    case "contenttype":
                        container.SetContentType(reader.GetString() ?? string.Empty);
                        break;

                    case "data":
                        // data is either null or a base64-encoded byte array
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

        // If we exit the loop normally, we never hit EndObject
        _logger.Error("EndObject token was expected but not found during ContentContainer deserialization.");
        throw new JsonException("Expected EndObject token for ContentContainer object.");
    }

    /// <summary>
    ///     Writes a <see cref="ContentContainer" /> to JSON format.
    /// </summary>
    /// <param name="writer">A <see cref="Utf8JsonWriter" /> to which the container is serialized.</param>
    /// <param name="value">The <see cref="ContentContainer" /> instance being serialized.</param>
    /// <param name="options">Serializer options, including custom converters.</param>
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
