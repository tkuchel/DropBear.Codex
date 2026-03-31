using DropBear.Codex.Files.StorageManagers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IO;

namespace DropBear.Codex.Files.Tests.StorageManagers;

public sealed class LocalStorageManagerTests : IDisposable
{
    private readonly string _rootDirectory = Path.GetFullPath(
        Path.Combine("DropBear.Codex.Files.Tests", Guid.NewGuid().ToString("N")),
        Path.GetTempPath());

    [Fact]
    public async Task WriteAsync_ShouldReject_RootedPathOutsideConfiguredRoot()
    {
        Directory.CreateDirectory(_rootDirectory);
        var storageManager = CreateStorageManager();
        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var outsidePath = Path.GetFullPath(Guid.NewGuid() + ".bin", Path.GetTempPath());

        var result = await storageManager.WriteAsync(outsidePath, stream);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("configured storage root");
    }

    [Fact]
    public async Task ReadAsync_ShouldReject_FileLargerThanBufferedReadLimit()
    {
        Directory.CreateDirectory(_rootDirectory);
        var storageManager = CreateStorageManager();
        var filePath = Path.Combine(_rootDirectory, "large.bin");

        await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fileStream.SetLength(101L * 1024L * 1024L);
        }

        var result = await storageManager.ReadAsync("large.bin");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("maximum buffered read size");
    }

    private LocalStorageManager CreateStorageManager()
    {
        return new LocalStorageManager(
            new RecyclableMemoryStreamManager(),
            NullLogger<LocalStorageManager>.Instance,
            _rootDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, true);
        }
    }
}
