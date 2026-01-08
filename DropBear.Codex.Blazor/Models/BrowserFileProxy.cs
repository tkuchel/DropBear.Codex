#region

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

#endregion

namespace DropBear.Codex.Blazor.Models;

public sealed class BrowserFileProxy : IBrowserFile, IAsyncDisposable
{
    private readonly string _contentType;
    private readonly string _extension;
    private readonly string? _fileKey; // Indicates the proxy was created from a file key.
    private readonly IJSObjectReference _jsModuleOrFileRef;
    private readonly DateTimeOffset _lastModified;
    private readonly string _name;
    private readonly long _size;
    private bool _disposed;

    /// <summary>
    ///     Constructor for proxies created from a JS file reference (e.g. via InputFile).
    /// </summary>
    public BrowserFileProxy(
        IJSObjectReference jsFileReference,
        string name,
        long size,
        string contentType,
        DateTimeOffset lastModified)
    {
        _jsModuleOrFileRef = jsFileReference ?? throw new ArgumentNullException(nameof(jsFileReference));
        _name = name;
        _size = size;
        _contentType = contentType;
        _lastModified = lastModified;
        _extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
    }

    /// <summary>
    ///     Private constructor used for creating a proxy from a file key.
    /// </summary>
    private BrowserFileProxy(
        string fileKey,
        IJSObjectReference jsModule,
        string name,
        long size,
        string contentType,
        DateTimeOffset lastModified,
        string extension)
    {
        _fileKey = fileKey;
        _jsModuleOrFileRef = jsModule ?? throw new ArgumentNullException(nameof(jsModule));
        _name = name;
        _size = size;
        _contentType = contentType;
        _lastModified = lastModified;
        _extension = extension ?? "";
    }

    public string Extension => _disposed
        ? throw new ObjectDisposedException(nameof(BrowserFileProxy))
        : _extension;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // If the proxy was created from a JS file reference (not from a file key),
            // dispose it. If it was created via a file key, _jsModuleOrFileRef is the shared JS module;
            // in that case, do not dispose it here.
            if (_fileKey is null)
            {
                await _jsModuleOrFileRef.DisposeAsync().ConfigureAwait(false);
            }
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

    /// <summary>
    ///     Opens a read stream for the file.
    /// </summary>
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
    ///     Creates a new BrowserFileProxy from a file key.
    ///     This method uses the provided JS module (the FileReaderHelpers module) to retrieve file information
    ///     and later to read file chunks.
    /// </summary>
    /// <param name="fileKey">The key referencing the stored file.</param>
    /// <param name="jsModule">The JS module reference (should be the FileReaderHelpers module).</param>
    public static async Task<BrowserFileProxy> CreateAsync(string fileKey, IJSObjectReference jsModule)
    {
        if (string.IsNullOrEmpty(fileKey))
        {
            throw new ArgumentException("Invalid file key", nameof(fileKey));
        }

        try
        {
            var fileInfo = await jsModule.InvokeAsync<FileInfoJson>("getFileInfoByKey", fileKey).ConfigureAwait(false);
            return new BrowserFileProxy(
                fileKey,
                jsModule,
                fileInfo.Name,
                fileInfo.Size,
                fileInfo.Type,
                DateTimeOffset.FromUnixTimeMilliseconds(fileInfo.LastModified),
                fileInfo.Extension
            );
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException("Failed to create BrowserFileProxy from file key", ex);
        }
    }


    /// <summary>
    ///     Reads a chunk of the file.
    /// </summary>
    private async Task<byte[]> ReadFileChunkAsync(long offset, int count, CancellationToken ct)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BrowserFileProxy));
        }

        // If the proxy was created from a file key, call a JS function that reads the chunk by key.
        if (_fileKey is not null)
        {
            var base64String = await _jsModuleOrFileRef.InvokeAsync<string>(
                "readFileChunkByKey",
                ct,
                _fileKey,
                offset,
                count
            ).ConfigureAwait(false);

            return Convert.FromBase64String(base64String);
        }

        // Otherwise, use the existing function that reads from a JS file reference.
        return await _jsModuleOrFileRef.InvokeAsync<byte[]>(
            "readFileChunk",
            ct,
            offset,
            count
        ).ConfigureAwait(false);
    }

    private sealed record FileInfoJson(
        string Name,
        string Extension,
        long Size,
        string Type,
        long LastModified
    );


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
                throw new ArgumentException("Invalid offset or count", nameof(count));
            }

            var bytesRemaining = Length - _position;
            if (bytesRemaining <= 0)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(count, bytesRemaining);
            var bytesRead = await _readAsync(buffer, offset, bytesToRead, cancellationToken).ConfigureAwait(false);
            _position += bytesRead;
            return bytesRead;
        }

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
}
