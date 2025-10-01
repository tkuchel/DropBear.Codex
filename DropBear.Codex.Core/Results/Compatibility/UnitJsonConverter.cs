#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     JSON converter for the Unit type.
///     Optimized for .NET 9 with minimal allocations.
/// </summary>
public sealed class UnitJsonConverter : JsonConverter<Unit>
{
    // Cached empty object bytes for writing
    private static ReadOnlySpan<byte> EmptyObjectUtf8 => "{}"u8;

    public override Unit Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // Unit has only one value, so any valid JSON represents it
        // Just skip whatever is there and return Unit.Value
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                // Null is acceptable for Unit
                break;

            case JsonTokenType.StartObject:
                // Skip the entire object
                reader.Skip();
                break;

            case JsonTokenType.StartArray:
                // Skip the entire array
                reader.Skip();
                break;

            default:
                // For any other token type, just consume it
                break;
        }

        return Unit.Value;
    }

    public override void Write(
        Utf8JsonWriter writer,
        Unit value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Write an empty object {} - most standard representation
        writer.WriteStartObject();
        writer.WriteEndObject();
    }

    public override Unit ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // Unit can't really be used as a property name, but if it is,
        // just return Unit.Value
        return Unit.Value;
    }

    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        Unit value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Write "()" as the property name representation of Unit
        writer.WritePropertyName("()");
    }
}
