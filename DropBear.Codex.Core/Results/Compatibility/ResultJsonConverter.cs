#region

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     JSON converter for Result{TError}.
/// </summary>
public class ResultJsonConverter<TError> : JsonConverter<Base.Result<TError>>
    where TError : ResultError
{
    public override Base.Result<TError> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        var state = ResultState.Success;
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
                case "error":
                    error = JsonSerializer.Deserialize<TError>(ref reader, options);
                    break;
            }
        }

        return state == ResultState.Success
            ? Base.Result<TError>.Success()
            : Base.Result<TError>.Failure(error!);
    }

    public override void Write(
        Utf8JsonWriter writer, Base.Result<TError> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("state", value.State.ToString());

        if (value.Error != null)
        {
            writer.WritePropertyName("error");
            JsonSerializer.Serialize(writer, value.Error, options);
        }

        writer.WriteEndObject();
    }
}
