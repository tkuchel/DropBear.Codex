#region

using System.ComponentModel;
using System.Text.Json.Serialization;

#endregion

namespace DropBear.Codex.Files.Enums;

/// <summary>
///     Specifies the strategy for file storage operations.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StorageStrategy
{
    /// <summary>
    ///     No operation will be performed.
    /// </summary>
    [Description("No operation")] NoOperation = 0,

    /// <summary>
    ///     File operations will be performed on local storage only.
    /// </summary>
    [Description("Local storage only")] LocalOnly = 1,

    /// <summary>
    ///     File operations will be performed on blob storage only.
    /// </summary>
    [Description("Blob storage only")] BlobOnly = 2
}
