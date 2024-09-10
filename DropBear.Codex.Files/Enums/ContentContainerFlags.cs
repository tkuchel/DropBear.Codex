#region

using System.ComponentModel;
using System.Text.Json.Serialization;

#endregion

namespace DropBear.Codex.Files.Enums;

/// <summary>
///     Specifies the flags that control the behavior of a content container.
/// </summary>
[Flags]
public enum ContentContainerFlags
{
    // Operation flags
    /// <summary>
    ///     No operation should be performed.
    /// </summary>
    [Description("No operation should be performed")] [JsonPropertyName("noOperation")]
    NoOperation = 1 << 0,

    // Processing flags
    /// <summary>
    ///     Serialization should be skipped.
    /// </summary>
    [Description("Serialization should be skipped")] [JsonPropertyName("noSerialization")]
    NoSerialization = 1 << 1,

    /// <summary>
    ///     Compression should be skipped.
    /// </summary>
    [Description("Compression should be skipped")] [JsonPropertyName("noCompression")]
    NoCompression = 1 << 2,

    /// <summary>
    ///     Encryption should be skipped.
    /// </summary>
    [Description("Encryption should be skipped")] [JsonPropertyName("noEncryption")]
    NoEncryption = 1 << 3,

    // State flags
    /// <summary>
    ///     Data has been set.
    /// </summary>
    [Description("Data has been set")] [JsonPropertyName("dataIsSet")]
    DataIsSet = 1 << 4,

    /// <summary>
    ///     Temporary data has been set.
    /// </summary>
    [Description("Temporary data has been set")] [JsonPropertyName("temporaryDataIsSet")]
    TemporaryDataIsSet = 1 << 5,

    // Action flags
    /// <summary>
    ///     Serialization should be performed.
    /// </summary>
    [Description("Serialization should be performed")] [JsonPropertyName("shouldSerialize")]
    ShouldSerialize = 1 << 6,

    /// <summary>
    ///     Compression should be performed.
    /// </summary>
    [Description("Compression should be performed")] [JsonPropertyName("shouldCompress")]
    ShouldCompress = 1 << 7,

    /// <summary>
    ///     Encryption should be performed.
    /// </summary>
    [Description("Encryption should be performed")] [JsonPropertyName("shouldEncrypt")]
    ShouldEncrypt = 1 << 8
}
