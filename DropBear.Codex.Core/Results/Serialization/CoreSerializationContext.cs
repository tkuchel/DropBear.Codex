#region

using System.Text.Json.Serialization;
using DropBear.Codex.Core.Envelopes;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Results.Serialization;

/// <summary>
///     JSON serialization context for DropBear.Codex.Core types.
///     Provides source-generated serialization for 20-30% performance improvement over reflection-based JSON serialization.
/// </summary>
/// <remarks>
///     This context includes all commonly serialized Core types:
///     - Result types (Result{TError}, Result{T, TError})
///     - Error types (SimpleError, CodedError, OperationError)
///     - Validation types (ValidationResult, ValidationError)
///     - Envelope types (EnvelopeDto{T})
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default,
    UseStringEnumConverter = true)]
// Result types
[JsonSerializable(typeof(Result<SimpleError>))]
[JsonSerializable(typeof(Result<CodedError>))]
[JsonSerializable(typeof(Result<OperationError>))]
[JsonSerializable(typeof(Result<ValidationError>))]
[JsonSerializable(typeof(Result<string, SimpleError>))]
[JsonSerializable(typeof(Result<string, CodedError>))]
[JsonSerializable(typeof(Result<string, OperationError>))]
[JsonSerializable(typeof(Result<int, SimpleError>))]
[JsonSerializable(typeof(Result<int, CodedError>))]
[JsonSerializable(typeof(Result<int, OperationError>))]
[JsonSerializable(typeof(Result<bool, SimpleError>))]
[JsonSerializable(typeof(Result<byte[], SimpleError>))]
// Error types
[JsonSerializable(typeof(SimpleError))]
[JsonSerializable(typeof(CodedError))]
[JsonSerializable(typeof(OperationError))]
[JsonSerializable(typeof(ResultError))]
// Validation types
[JsonSerializable(typeof(ValidationResult))]
[JsonSerializable(typeof(ValidationError))]
// Envelope types
[JsonSerializable(typeof(EnvelopeDto<string>))]
[JsonSerializable(typeof(EnvelopeDto<byte[]>))]
[JsonSerializable(typeof(EnvelopeDto<object>))]
// Common collections
[JsonSerializable(typeof(List<SimpleError>))]
[JsonSerializable(typeof(List<ValidationError>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class CoreSerializationContext : JsonSerializerContext
{
}
