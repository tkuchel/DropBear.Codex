#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     JSON converter for ValidationResult.
/// </summary>
public sealed class ValidationResultJsonConverter : JsonConverter<ValidationResult>
{
    public override ValidationResult Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.Parse(reader.GetString() ?? "{}");
        var root = document.RootElement;

        var isValid = root.GetProperty("isValid").GetBoolean();
        if (isValid)
        {
            return ValidationResult.Success;
        }

        var message = root.GetProperty("errorMessage").GetString() ?? "Validation failed";
        return ValidationResult.Failed(message);
    }

    public override void Write(
        Utf8JsonWriter writer,
        ValidationResult value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("isValid", value.IsValid);
        writer.WriteString("errorMessage", value.ErrorMessage);

        if (value.Error != null)
        {
            writer.WritePropertyName("details");
            JsonSerializer.Serialize(writer, value.Error, options);
        }

        writer.WriteEndObject();
    }
}
