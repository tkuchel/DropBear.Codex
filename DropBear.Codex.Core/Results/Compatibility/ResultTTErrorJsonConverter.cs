#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Compatibility;

/// <summary>
///     JSON converter for Result{T, TError}.
///     Optimized for .NET 9 with enhanced type safety.
/// </summary>
public sealed class ResultTTErrorJsonConverter<T, TError> : JsonConverter<Result<T, TError>>
    where TError : ResultError
{
    private static readonly JsonEncodedText StatePropertyName = JsonEncodedText.Encode("state");
    private static readonly JsonEncodedText ValuePropertyName = JsonEncodedText.Encode("value");
    private static readonly JsonEncodedText ErrorPropertyName = JsonEncodedText.Encode("error");
    private static readonly JsonEncodedText ExceptionPropertyName = JsonEncodedText.Encode("exception");

    public override Result<T, TError> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        ResultState state = ResultState.Success;
        T? value = default;
        TError? error = null;
        string? exceptionMessage = null;
        bool hasValue = false;

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

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName?.ToLowerInvariant())
            {
                case "state":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        var stateString = reader.GetString();
                        if (!Enum.TryParse<ResultState>(stateString, ignoreCase: true, out state))
                        {
                            throw new JsonException($"Invalid ResultState value: {stateString}");
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.Number)
                    {
                        state = (ResultState)reader.GetInt32();
                    }
                    break;

                case "value":
                    if (reader.TokenType != JsonTokenType.Null)
                    {
                        value = JsonSerializer.Deserialize<T>(ref reader, options);
                        hasValue = true;
                    }
                    break;

                case "error":
                    if (reader.TokenType != JsonTokenType.Null)
                    {
                        error = JsonSerializer.Deserialize<TError>(ref reader, options);
                    }
                    break;

                case "exception":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        exceptionMessage = reader.GetString();
                    }
                    else if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        reader.Skip();
                    }
                    break;

                default:
                    reader.Skip();
                    break;
            }
        }

        // Reconstruct exception if message was present
        Exception? exception = null;
        if (!string.IsNullOrEmpty(exceptionMessage))
        {
            exception = new InvalidOperationException(exceptionMessage);
        }

        // Return appropriate result based on state and available data
        if (state == ResultState.Success && hasValue)
        {
            return Result<T, TError>.Success(value!);
        }

        if (state == ResultState.Success && !hasValue)
        {
            throw new JsonException("Success state requires a value");
        }

        // Handle states with both value and error
        if (state == ResultState.Warning && hasValue && error != null)
        {
            return Result<T, TError>.Warning(value!, error);
        }

        if (state == ResultState.PartialSuccess && hasValue && error != null)
        {
            return Result<T, TError>.PartialSuccess(value!, error);
        }

        // Failure or other states
        if (error == null)
        {
            error = CreateDefaultError();
        }

        return Result<T, TError>.Failure(error, exception);
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
        writer.WriteString(StatePropertyName, value.State.ToString());

        // Write value if present and successful
        if (value.IsSuccess && value.Value != null)
        {
            writer.WritePropertyName(ValuePropertyName);
            JsonSerializer.Serialize(writer, value.Value, options);
        }

        // Write error if present
        if (value.Error != null)
        {
            writer.WritePropertyName(ErrorPropertyName);
            JsonSerializer.Serialize(writer, value.Error, options);
        }

        // Write exception message if present
        if (value.Exception != null)
        {
            writer.WriteString(ExceptionPropertyName, value.Exception.Message);
        }

        writer.WriteEndObject();
    }

    private static TError CreateDefaultError()
    {
        return (TError)Activator.CreateInstance(typeof(TError), "Deserialization error: no error information provided")!;
    }
}
