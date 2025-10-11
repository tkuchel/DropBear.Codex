#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Results.Serialization;

/// <summary>
///     JSON converter for ValidationResult types.
///     Handles serialization and deserialization of validation results.
/// </summary>
public sealed class ValidationResultJsonConverter : JsonConverter<ValidationResult>
{
    public override ValidationResult Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }

        bool? isValid = null;
        List<ValidationError>? errors = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "isValid":
                    isValid = reader.GetBoolean();
                    break;
                case "errors":
                    errors = JsonSerializer.Deserialize<List<ValidationError>>(ref reader, options);
                    break;
            }
        }

        if (!isValid.HasValue)
        {
            throw new JsonException("Missing required 'isValid' property");
        }

        // Reconstruct the validation result
        if (isValid.Value)
        {
            return ValidationResult.Success;
        }

        if (errors is null || errors.Count == 0)
        {
            throw new JsonException("Invalid validation result must have errors");
        }

        return ValidationResult.Failed(errors);
    }

    public override void Write(
        Utf8JsonWriter writer,
        ValidationResult value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();

        // Write isValid
        writer.WritePropertyName("isValid");
        writer.WriteBooleanValue(value.IsValid);

        // Write errors if present
        if (value is { IsValid: false, Errors.Count: > 0 })
        {
            writer.WritePropertyName("errors");
            JsonSerializer.Serialize(writer, value.Errors, options);
        }

        writer.WriteEndObject();
    }
}
