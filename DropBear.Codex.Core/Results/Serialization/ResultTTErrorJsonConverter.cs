#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Serialization;

/// <summary>
///     JSON converter for Result{T, TError} types.
///     Handles serialization and deserialization of result objects with values.
/// </summary>
public sealed class ResultTTErrorJsonConverter<T, TError> : JsonConverter<Result<T, TError>>
    where TError : ResultError
{
    public override Result<T, TError> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }

        ResultState? state = null;
        T? value = default;
        TError? error = default;

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
                case "state":
                    state = JsonSerializer.Deserialize<ResultState>(ref reader, options);
                    break;
                case "value":
                    value = JsonSerializer.Deserialize<T>(ref reader, options);
                    break;
                case "error":
                    error = JsonSerializer.Deserialize<TError>(ref reader, options);
                    break;
            }
        }

        if (!state.HasValue)
        {
            throw new JsonException("Missing required 'state' property");
        }

        // Reconstruct the result based on state
        return state.Value switch
        {
            ResultState.Success => Result<T, TError>.Success(value!),
            ResultState.Failure => Result<T, TError>.Failure(error!),
            ResultState.Warning when value is not null => Result<T, TError>.Warning(value, error!),
            ResultState.Warning => Result<T, TError>.Warning(error!),
            ResultState.PartialSuccess => Result<T, TError>.PartialSuccess(value!, error!),
            ResultState.Cancelled when value is not null => Result<T, TError>.Cancelled(value, error!),
            ResultState.Cancelled => Result<T, TError>.Cancelled(error!),
            ResultState.Pending when value is not null => Result<T, TError>.Pending(value, error!),
            ResultState.Pending => Result<T, TError>.Pending(error!),
            ResultState.NoOp when value is not null => Result<T, TError>.NoOp(value, error!),
            ResultState.NoOp => Result<T, TError>.NoOp(error!),
            _ => throw new JsonException($"Unknown result state: {state.Value}")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        Result<T, TError> value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();

        // Write state
        writer.WritePropertyName("state");
        JsonSerializer.Serialize(writer, value.State, options);

        // Write value if present
        if (value.IsSuccess && value.Value is not null)
        {
            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, value.Value, options);
        }

        // Write error if present
        if (value.Error is not null)
        {
            writer.WritePropertyName("error");
            JsonSerializer.Serialize(writer, value.Error, options);
        }

        writer.WriteEndObject();
    }
}
