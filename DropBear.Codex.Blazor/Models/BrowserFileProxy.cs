#region

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Models;

public sealed class BrowserFileProxy : IBrowserFile, IAsyncDisposable
{
    private readonly string _contentType;
    private readonly IJSObjectReference _jsFileReference;
    private readonly DateTimeOffset _lastModified;
    private readonly string _name;
    private readonly long _size;
    private bool _disposed;

    public BrowserFileProxy(
        IJSObjectReference jsFileReference,
        string name,
        long size,
        string contentType,
        DateTimeOffset lastModified)
    {
        _jsFileReference = jsFileReference ?? throw new ArgumentNullException(nameof(jsFileReference));
        _name = name;
        _size = size;
        _contentType = contentType;
        _lastModified = lastModified;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await _jsFileReference.DisposeAsync();
        }
        finally
        {
            _disposed = true;
        }
    }

    public string Name => _disposed
        ? throw new ObjectDisposedException(nameof(BrowserFileProxy))
        : _name;

    public DateTimeOffset LastModified => _disposed
        ? throw new ObjectDisposedException(nameof(BrowserFileProxy))
        : _lastModified;

    public long Size => _disposed
        ? throw new ObjectDisposedException(nameof(BrowserFileProxy))
        : _size;

    public string ContentType => _disposed
        ? throw new ObjectDisposedException(nameof(BrowserFileProxy))
        : _contentType;

    public Stream OpenReadStream(
        long maxAllowedSize = 512000,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BrowserFileProxy));
        }

        if (_size > maxAllowedSize)
        {
            throw new IOException($"File size {_size} bytes exceeds maximum allowed size {maxAllowedSize} bytes");
        }

        // Create an adapter stream that handles async operations and uses the buffer correctly
        return new BrowserFileProxyStream(
            async (buffer, offset, count, ct) =>
            {
                var chunk = await ReadFileChunkAsync(offset, count, ct).ConfigureAwait(false);
                Array.Copy(chunk, 0, buffer, offset, chunk.Length);
                return chunk.Length;
            },
            _size
        );
    }

    /// <summary>
    ///     Reads a chunk of the file from JavaScript
    /// </summary>
    /// <param name="offset">The starting position in the file</param>
    /// <param name="count">The number of bytes to read</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The bytes read from the file</returns>
    private async Task<byte[]> ReadFileChunkAsync(long offset, int count, CancellationToken ct)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BrowserFileProxy));
        }

        return await _jsFileReference.InvokeAsync<byte[]>(
            "readFileChunk",
            ct,
            offset,
            count
        ).ConfigureAwait(false);
    }

    /// <summary>
    ///     Creates a new BrowserFileProxy from a JavaScript file reference
    /// </summary>
    public static async Task<BrowserFileProxy> CreateAsync(IJSObjectReference jsFileReference)
    {
        ArgumentNullException.ThrowIfNull(jsFileReference);

        try
        {
            var fileInfo = await jsFileReference.InvokeAsync<FileInfoJson>("getFileInfo");
            return new BrowserFileProxy(
                jsFileReference,
                fileInfo.Name,
                fileInfo.Size,
                fileInfo.Type,
                DateTimeOffset.FromUnixTimeMilliseconds(fileInfo.LastModified));
        }
        catch (JSException ex)
        {
            await jsFileReference.DisposeAsync();
            throw new InvalidOperationException("Failed to create BrowserFileProxy", ex);
        }
    }

    /// <summary>
    ///     Custom stream implementation that bridges sync/async operations
    /// </summary>
    private sealed class BrowserFileProxyStream : Stream
    {
        private readonly Func<byte[], int, int, CancellationToken, Task<int>> _readAsync;
        private long _position;

        public BrowserFileProxyStream(
            Func<byte[], int, int, CancellationToken, Task<int>> readAsync,
            long length)
        {
            _readAsync = readAsync ?? throw new ArgumentNullException(nameof(readAsync));
            Length = length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length { get; }

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (buffer.Length - offset < count)
            {
                throw new ArgumentException("Invalid offset or count");
            }

            var bytesRemaining = Length - _position;
            if (bytesRemaining <= 0)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(count, bytesRemaining);
            var bytesRead = await _readAsync(buffer, offset, bytesToRead, cancellationToken);
            _position += bytesRead;
            return bytesRead;
        }

        // Other required overrides (throw NotSupportedException)
        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("Use ReadAsync instead");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record FileInfoJson(
        string Name,
        long Size,
        string Type,
        long LastModified);
}
