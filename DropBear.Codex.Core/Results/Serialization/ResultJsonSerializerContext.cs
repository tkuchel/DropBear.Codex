#region

using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Compatibility;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Results.Serialization;

/// <summary>
///     Source-generated JSON serialization context for Result types.
///     Provides optimal performance for JSON serialization in .NET 9.
/// </summary>
/// <remarks>
///     To use this context:
///     <code>
///     var options = new JsonSerializerOptions
///     {
///         TypeInfoResolver = ResultJsonSerializerContext.Default
///     };
///     var json = JsonSerializer.Serialize(result, options);
///     </code>
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ValidationResult))]
[JsonSerializable(typeof(ValidationError))]
[JsonSerializable(typeof(Unit))]
[JsonSerializable(typeof(LegacyError))]
[JsonSerializable(typeof(Result))]
[JsonSerializable(typeof(Compatibility.Result<string>))]
[JsonSerializable(typeof(Compatibility.Result<int>))]
[JsonSerializable(typeof(Compatibility.Result<bool>))]
public partial class ResultJsonSerializerContext : JsonSerializerContext
{
    /// <summary>
    ///     Creates options configured to use this source-generated context.
    /// </summary>
    public static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            TypeInfoResolver = Default,
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
