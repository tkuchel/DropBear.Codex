#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Serialization;

/// <summary>
///     JSON converter for Result{TError} types.
///     Handles serialization and deserialization of error-only results.
/// </summary>
public sealed class ResultTErrorJsonConverter<TError> : JsonConverter<Result<TError>>
    where TError : ResultError
{
    public override Result<TError> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }

        ResultState? state = null;
        TError? error = null;

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
            ResultState.Success => Result<TError>.Success(),
            ResultState.Failure => Result<TError>.Failure(error!),
            ResultState.Warning => Result<TError>.Warning(error!),
            ResultState.Cancelled => Result<TError>.Cancelled(error!),
            ResultState.Pending => Result<TError>.Pending(error!),
            ResultState.NoOp => Result<TError>.NoOp(error!),
            _ => throw new JsonException($"Unknown result state: {state.Value}")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        Result<TError> value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();

        // Write state
        writer.WritePropertyName("state");
        JsonSerializer.Serialize(writer, value.State, options);

        // Write error if present
        if (value.Error is not null)
        {
            writer.WritePropertyName("error");
            JsonSerializer.Serialize(writer, value.Error, options);
        }

        writer.WriteEndObject();
    }
}
