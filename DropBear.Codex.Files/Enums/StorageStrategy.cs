namespace DropBear.Codex.Files.Enums;

/// <summary>
///     Specifies the strategy for file storage operations.
/// </summary>
public enum StorageStrategy
{
    /// <summary>
    ///     No operation will be performed.
    /// </summary>
    NoOperation,

    /// <summary>
    ///     File operations will be performed on local storage only.
    /// </summary>
    LocalOnly,

    /// <summary>
    ///     File operations will be performed on blob storage only.
    /// </summary>
    BlobOnly
}
