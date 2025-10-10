#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Serialization;

/// <summary>
///     A JSON converter for <see cref="Result{TError}" /> that handles error-only result shapes.
/// </summary>
/// <typeparam name="TError">
///     The concrete error type, derived from <see cref="ResultError" />, to serialize and deserialize.
/// </typeparam>
/// <remarks>
///     <para>
///         The on-wire JSON shape is:
///     </para>
///     <code language="json">
///     {
///       "state": "Failure", // or numeric if enum-as-number is configured
///       "error": { /* TError JSON, optional depending on state */ }
///     }
///     </code>
///     <para>
///         The <c>state</c> property is required. The <c>error</c> property is included for non-success states.
///         Unknown properties are ignored.
///     </para>
///     <para>
///         For human-readable enum values, register <see cref="JsonStringEnumConverter" />.
///     </para>
/// </remarks>
/// <example>
///     <code><![CDATA[
///     var options = new JsonSerializerOptions
///     {
///         Converters =
///         {
///             new JsonStringEnumConverter(), // optional, for ResultState
///             new ResultTErrorJsonConverter<MyError>() // this converter
///         }
///     };
/// 
///     // Serialize
///     Result<MyError> failure = Result<MyError>.Failure(new MyError(...));
///     string json = JsonSerializer.Serialize(failure, options);
/// 
///     // Deserialize
///     Result<MyError> roundTripped = JsonSerializer.Deserialize<Result<MyError>>(json, options)!;
///     ]]></code>
/// </example>
/// <seealso cref="Result{TError}" />
/// <seealso cref="ResultError" />
/// <seealso cref="ResultState" />
/// <seealso cref="JsonConverter{T}" />
public sealed class ResultTErrorJsonConverter<TError> : JsonConverter<Result<TError>>
    where TError : ResultError
{
    /// <summary>
    ///     Reads a <see cref="Result{TError}" /> instance from JSON.
    /// </summary>
    /// <param name="reader">A by-ref <see cref="Utf8JsonReader" /> positioned at the start of the value.</param>
    /// <param name="typeToConvert">The target type. For this converter it will be <c>typeof(Result&lt;TError&gt;)</c>.</param>
    /// <param name="options">Serializer options to use for nested values and enums.</param>
    /// <returns>
    ///     The deserialized <see cref="Result{TError}" /> reconstructed from its <see cref="ResultState" /> and optional error
    ///     payload.
    /// </returns>
    /// <exception cref="JsonException">
    ///     Thrown when the JSON is not an object, when the required <c>state</c> property is missing,
    ///     or when the state value is unknown.
    /// </exception>
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
                default:
                    // Skip unknown property values (object/array scalars) to keep the reader in sync.
                    reader.Skip();
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

    /// <summary>
    ///     Writes a <see cref="Result{TError}" /> instance to JSON with <c>state</c> and optional <c>error</c> properties.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter" /> to write to. Must not be <c>null</c>.</param>
    /// <param name="value">The <see cref="Result{TError}" /> to serialize. Must not be <c>null</c>.</param>
    /// <param name="options">Serializer options to use for nested values and enums.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="writer" /> or <paramref name="value" /> is <c>null</c>.
    /// </exception>
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
