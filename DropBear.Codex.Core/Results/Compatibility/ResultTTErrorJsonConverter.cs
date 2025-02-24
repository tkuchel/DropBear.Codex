#region

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     JSON converter for Result{T, TError}.
/// </summary>
public class ResultTTErrorJsonConverter<T, TError> : JsonConverter<Result<T, TError>>
    where TError : ResultError
{
    public override Result<T, TError> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        var state = ResultState.Success;
        T? value = default;
        TError? error = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName?.ToLower(CultureInfo.InvariantCulture))
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

        return state == ResultState.Success
            ? Result<T, TError>.Success(value!)
            : Result<T, TError>.Failure(error!);
    }

    public override void Write(
        Utf8JsonWriter writer,
        Result<T, TError> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("state", value.State.ToString());

        if (value.IsSuccess && value.Value != null)
        {
            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, value.Value, options);
        }

        if (value.Error != null)
        {
            writer.WritePropertyName("error");
            JsonSerializer.Serialize(writer, value.Error, options);
        }

        writer.WriteEndObject();
    }
}
