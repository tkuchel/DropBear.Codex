#region

using System.Text.Json.Serialization;
using DropBear.Codex.Blazor.Errors;
using DropBear.Codex.Blazor.Models;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Blazor.Serialization;

/// <summary>
///     JSON serialization context for DropBear.Codex.Blazor types.
///     Provides source-generated serialization for 20-30% performance improvement over reflection-based JSON serialization.
/// </summary>
/// <remarks>
///     This context includes all commonly serialized Blazor types:
///     - Blazor error types (FileUploadError, JsInteropError, ComponentError, etc.)
///     - UploadResult for file upload operations
///     - Common Result types used in Blazor components
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default,
    UseStringEnumConverter = true)]
// Blazor error types
[JsonSerializable(typeof(FileUploadError))]
[JsonSerializable(typeof(FileDownloadError))]
[JsonSerializable(typeof(JsInteropError))]
[JsonSerializable(typeof(JsInitializationError))]
[JsonSerializable(typeof(ModalError))]
[JsonSerializable(typeof(AlertError))]
[JsonSerializable(typeof(ComponentError))]
[JsonSerializable(typeof(DataFetchError))]
[JsonSerializable(typeof(DataGridError))]
[JsonSerializable(typeof(ProgressManagerError))]
[JsonSerializable(typeof(SnackbarError))]
[JsonSerializable(typeof(IconError))]
// Upload result
[JsonSerializable(typeof(UploadResult))]
// Common Result types for Blazor operations
[JsonSerializable(typeof(Result<Unit, FileUploadError>))]
[JsonSerializable(typeof(Result<Unit, JsInteropError>))]
[JsonSerializable(typeof(Result<Unit, ComponentError>))]
[JsonSerializable(typeof(Result<string, FileUploadError>))]
[JsonSerializable(typeof(Result<string, SimpleError>))]
[JsonSerializable(typeof(Result<byte[], FileDownloadError>))]
// Common collections
[JsonSerializable(typeof(List<FileUploadError>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class BlazorSerializationContext : JsonSerializerContext
{
}
