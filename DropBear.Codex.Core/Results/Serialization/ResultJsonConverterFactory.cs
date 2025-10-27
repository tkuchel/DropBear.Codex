#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Serialization;

/// <summary>
///     Factory for creating Result JSON converters.
///     Handles generic type instantiation correctly.
/// </summary>
public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    /// <summary>
    ///  Determines if the given type can be converted by this factory.
    /// </summary>
    /// <param name="typeToConvert">The type to try and convert.</param>
    /// <returns>A bool representing if the given type can be converted by this factory.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        if (!typeToConvert.IsGenericType)
        {
            return false;
        }

        var genericType = typeToConvert.GetGenericTypeDefinition();

        // Check if it's Result<T, TError> (2 params) or Result<TError> (1 param)
        return genericType == typeof(Result<,>) || genericType == typeof(Result<>);
    }

    /// <summary>
    ///  Creates a converter for the given type.
    /// </summary>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The Json serializer options to be used during the conversion.</param>
    /// <returns>A Json converter for the specified type using the specified Json serializer options.</returns>
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        var genericArgs = typeToConvert.GetGenericArguments();

        if (genericArgs.Length == 2)
        {
            // Result<T, TError>
            var converterType = typeof(ResultTTErrorJsonConverter<,>)
                .MakeGenericType(genericArgs[0], genericArgs[1]);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        if (genericArgs.Length == 1)
        {
            // Result<TError>
            var converterType = typeof(ResultTErrorJsonConverter<>)
                .MakeGenericType(genericArgs[0]);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        return null;
    }
}
