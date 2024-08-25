#region

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

namespace DropBear.Codex.Utilities.ConfigurationPresets;

/// <summary>
///     Provides preset configurations for <see cref="JsonSerializerOptions" /> used in JSON serialization.
/// </summary>
public static class JsonSerializerPresets
{
    /// <summary>
    ///     Creates a new instance of <see cref="JsonSerializerOptions" /> with default settings.
    /// </summary>
    /// <returns>A <see cref="JsonSerializerOptions" /> instance with predefined settings.</returns>
    public static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true, // Indent the output for better readability
            IncludeFields = true, // Include public fields in serialization
            PropertyNameCaseInsensitive = true, // Ignore case when matching property names
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // Ignore null values when serializing
            NumberHandling = JsonNumberHandling.AllowReadingFromString, // Allow reading numbers from strings
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Use a more relaxed JSON encoder
            ReferenceHandler = ReferenceHandler.Preserve, // Preserve reference relationships
            MaxDepth = 64, // Set a maximum depth to avoid StackOverflowExceptions
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode, // Handle unknown types as JsonNode
            Converters =
            {
                new JsonStringEnumConverter() // Use a converter for enums
                // Add more custom converters here if needed
            }
        };

        return options;
    }
}
