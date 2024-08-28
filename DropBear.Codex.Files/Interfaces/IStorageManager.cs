#region

using DropBear.Codex.Core;

#endregion

namespace DropBear.Codex.Files.Interfaces;

public interface IStorageManager
{
    Task<Result> WriteAsync(string identifier, Stream dataStream, string? subDirectory = null);
    Task<Result<Stream>> ReadAsync(string identifier, string? subDirectory = null);
    Task<Result> UpdateAsync(string identifier, Stream newDataStream, string? subDirectory = null);
    Task<Result> DeleteAsync(string identifier, string? subDirectory = null);
}
