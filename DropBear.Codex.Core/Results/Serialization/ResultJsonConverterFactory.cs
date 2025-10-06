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
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
        {
            return false;
        }

        var genericType = typeToConvert.GetGenericTypeDefinition();

        // Check if it's Result<T, TError> or Result<TError>
        return genericType == typeof(Result<,>) ||
               (genericType.BaseType?.IsGenericType == true &&
                genericType.BaseType.GetGenericTypeDefinition() == typeof(Result<>));
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
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
