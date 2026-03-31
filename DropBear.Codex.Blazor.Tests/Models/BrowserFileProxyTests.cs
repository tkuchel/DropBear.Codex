using DropBear.Codex.Blazor.Models;
using FluentAssertions;
using Microsoft.JSInterop;
using Moq;

namespace DropBear.Codex.Blazor.Tests.Models;

public sealed class BrowserFileProxyTests
{
    [Fact]
    public async Task OpenReadStream_ShouldRejectChunksLargerThanRequested()
    {
        var jsModule = new Mock<IJSObjectReference>();
        jsModule
            .Setup(module => module.InvokeAsync<byte[]>(
                "readFileChunk",
                It.IsAny<CancellationToken>(),
                It.IsAny<object?[]>()))
            .Returns((string _, CancellationToken _, object?[] args) =>
            {
                var requestedCount = (int)args[1]!;
                return ValueTask.FromResult(new byte[requestedCount + 1]);
            });

        var proxy = new BrowserFileProxy(
            jsModule.Object,
            "report.pdf",
            10,
            "application/pdf",
            DateTimeOffset.UtcNow);

        using var stream = proxy.OpenReadStream(maxAllowedSize: 10);
        var buffer = new byte[4];

        var act = async () => await stream.ReadAsync(buffer, 0, buffer.Length);

        await act.Should().ThrowAsync<IOException>()
            .WithMessage("*returned*requested chunk*");
    }

    [Fact]
    public async Task OpenReadStream_ShouldRejectWhenJsReturnsMoreBytesThanDeclaredLength()
    {
        var stream = new OversizedReadStream(async (_, _, _, _) =>
        {
            await Task.CompletedTask;
            return 6;
        }, 5);

        var buffer = new byte[8];
        var act = async () => await stream.ReadAsync(buffer, 0, buffer.Length);

        await act.Should().ThrowAsync<IOException>()
            .WithMessage("*at most*allowed*");
    }

    private sealed class OversizedReadStream(
        Func<byte[], int, int, CancellationToken, Task<int>> readAsync,
        long length) : Stream
    {
        private readonly Func<byte[], int, int, CancellationToken, Task<int>> _readAsync = readAsync;
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length { get; } = length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesRemaining = Length - _position;
            if (bytesRemaining <= 0)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(count, bytesRemaining);
            var bytesRead = await _readAsync(buffer, offset, bytesToRead, cancellationToken).ConfigureAwait(false);

            if (bytesRead < 0)
            {
                throw new IOException("JavaScript returned a negative byte count.");
            }

            if (bytesRead > bytesToRead || bytesRead > bytesRemaining)
            {
                throw new IOException(
                    $"JavaScript returned {bytesRead} bytes when at most {bytesToRead} bytes were allowed.");
            }

            _position += bytesRead;
            return bytesRead;
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
