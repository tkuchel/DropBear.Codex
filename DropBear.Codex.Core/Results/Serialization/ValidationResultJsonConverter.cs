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
    /// <summary>
    ///     Reads and deserializes a JSON representation into a ValidationResult instance.
    /// </summary>
    /// <param name="reader">The Utf8JsonReader to read from.</param>
    /// <param name="typeToConvert">The type of object to convert.</param>
    /// <param name="options">The serializer options to use.</param>
    /// <returns>A ValidationResult instance deserialized from the JSON.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or missing required properties.</exception>
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

    /// <summary>
    ///     Writes a ValidationResult instance as JSON.
    /// </summary>
    /// <param name="writer">The Utf8JsonWriter to write to.</param>
    /// <param name="value">The ValidationResult instance to serialize.</param>
    /// <param name="options">The serializer options to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when writer or value is null.</exception>
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
