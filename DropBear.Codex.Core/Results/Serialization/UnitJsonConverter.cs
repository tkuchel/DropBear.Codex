#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Serialization;

/// <summary>
///     JSON converter for the Unit type.
///     Serializes Unit as an empty object {}.
/// </summary>
public sealed class UnitJsonConverter : JsonConverter<Unit>
{
    /// <summary>
    ///     Reads and deserializes a JSON representation into a Unit instance.
    /// </summary>
    /// <param name="reader">The Utf8JsonReader to read from.</param>
    /// <param name="typeToConvert">The type of object to convert.</param>
    /// <param name="options">The serializer options to use.</param>
    /// <returns>The single Unit.Value instance.</returns>
    public override Unit Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // Unit has only one value, so just skip the JSON and return it
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            reader.Read(); // Read through the object
            while (reader.TokenType != JsonTokenType.EndObject)
            {
                reader.Read();
            }
        }

        return Unit.Value;
    }

    /// <summary>
    ///     Writes a Unit instance as JSON (empty object).
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to write to.</param>
    /// <param name="value">The Unit instance to serialize.</param>
    /// <param name="options">The serializer options to use.</param>
    public override void Write(
        Utf8JsonWriter writer,
        Unit value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Write Unit as an empty object
        writer.WriteStartObject();
        writer.WriteEndObject();
    }
}
