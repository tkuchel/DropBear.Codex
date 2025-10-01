#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     JSON converter for ValidationResult.
///     Optimized for .NET 9 with enhanced validation.
/// </summary>
public sealed class ValidationResultJsonConverter : JsonConverter<ValidationResult>
{
    private static readonly JsonEncodedText IsValidPropertyName = JsonEncodedText.Encode("isValid");
    private static readonly JsonEncodedText ErrorMessagePropertyName = JsonEncodedText.Encode("errorMessage");
    private static readonly JsonEncodedText ErrorPropertyName = JsonEncodedText.Encode("error");
    private static readonly JsonEncodedText PropertyNamePropertyName = JsonEncodedText.Encode("propertyName");
    private static readonly JsonEncodedText ValidationRulePropertyName = JsonEncodedText.Encode("validationRule");

    public override ValidationResult Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        bool isValid = true;
        string? errorMessage = null;
        string? propertyName = null;
        string? validationRule = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            var propertyNameValue = reader.GetString();
            reader.Read();

            switch (propertyNameValue?.ToLowerInvariant())
            {
                case "isvalid":
                    if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
                    {
                        isValid = reader.GetBoolean();
                    }
                    else
                    {
                        throw new JsonException("isValid property must be a boolean");
                    }
                    break;

                case "errormessage":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        errorMessage = reader.GetString();
                    }
                    else if (reader.TokenType != JsonTokenType.Null)
                    {
                        throw new JsonException("errorMessage property must be a string or null");
                    }
                    break;

                case "propertyname":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        propertyName = reader.GetString();
                    }
                    break;

                case "validationrule":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        validationRule = reader.GetString();
                    }
                    break;

                case "error":
                    // Skip detailed error object if present, we'll use errorMessage
                    reader.Skip();
                    break;

                default:
                    // Skip unknown properties
                    reader.Skip();
                    break;
            }
        }

        if (isValid)
        {
            return ValidationResult.Success;
        }

        // Create appropriate validation error based on available information
        if (!string.IsNullOrWhiteSpace(propertyName))
        {
            return ValidationResult.PropertyFailed(
                propertyName,
                errorMessage ?? "Validation failed");
        }

        if (!string.IsNullOrWhiteSpace(validationRule))
        {
            return ValidationResult.RuleFailed(
                validationRule,
                errorMessage ?? "Validation failed");
        }

        return ValidationResult.Failed(errorMessage ?? "Validation failed");
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
        writer.WriteBoolean(IsValidPropertyName, value.IsValid);

        // Write errorMessage if present
        if (!value.IsValid)
        {
            writer.WriteString(ErrorMessagePropertyName, value.ErrorMessage);

            // Write detailed error information if available
            if (value.Error != null)
            {
                // Write property name if available
                if (!string.IsNullOrWhiteSpace(value.Error.PropertyName))
                {
                    writer.WriteString(PropertyNamePropertyName, value.Error.PropertyName);
                }

                // Write validation rule if available
                if (!string.IsNullOrWhiteSpace(value.Error.ValidationRule))
                {
                    writer.WriteString(ValidationRulePropertyName, value.Error.ValidationRule);
                }

                // Write full error object for detailed information
                writer.WritePropertyName(ErrorPropertyName);
                JsonSerializer.Serialize(writer, value.Error, options);
            }
        }

        writer.WriteEndObject();
    }
}
