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

    public override void Write(
        Utf8JsonWriter writer,
        Unit value,
        JsonSerializerOptions options)
    {
        // Write Unit as an empty object
        writer.WriteStartObject();
        writer.WriteEndObject();
    }
}
