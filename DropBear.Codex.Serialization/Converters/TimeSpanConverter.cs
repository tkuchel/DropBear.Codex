#region

using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

namespace DropBear.Codex.Serialization.Converters;

/// <summary>
///     JSON converter for TimeSpan values.
/// </summary>
public class TimeSpanConverter : JsonConverter<TimeSpan>
{
    /// <summary>
    ///     Reads and converts the JSON to a TimeSpan.
    /// </summary>
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString() ?? string.Empty;
            return TimeSpan.Parse(stringValue);
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            // Treat as total milliseconds
            return TimeSpan.FromMilliseconds(reader.GetDouble());
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to TimeSpan");
    }

    /// <summary>
    ///     Writes a TimeSpan as JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
