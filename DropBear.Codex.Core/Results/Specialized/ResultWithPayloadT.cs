#region

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Core.Results.Specialized;

/// <summary>
///     Represents a Result that includes a compressed and hashed payload
/// </summary>
public sealed class ResultWithPayload<T> : Result<T, PayloadError>
{
    private const int BufferSize = 81920; // 80KB buffer for optimal compression performance

    #region Constructor

    private ResultWithPayload(
        T? value,
        ReadOnlyMemory<byte> payload,
        string hash,
        ResultState state,
        PayloadError? error = null,
        Exception? exception = null)
        : base(value!, state, error, exception)
    {
        Payload = payload;
        Hash = hash;
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Decompresses and deserializes the payload to a new type
    /// </summary>
    public async ValueTask<Result<TOut, PayloadError>> DecompressAndDeserializeAsync<TOut>(
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSuccess)
        {
            return Result<TOut, PayloadError>.Failure(
                new PayloadError("Cannot decompress failed result"));
        }

        if (!IsValid)
        {
            return Result<TOut, PayloadError>.Failure(
                new PayloadError("Payload validation failed"));
        }

        try
        {
            using var decompressedStream = await DecompressToStreamAsync(
                Payload,
                cancellationToken).ConfigureAwait(false);

            var result = await JsonSerializer.DeserializeAsync<TOut>(
                decompressedStream,
                options,
                cancellationToken).ConfigureAwait(false);

            return result is not null
                ? Result<TOut, PayloadError>.Success(result)
                : Result<TOut, PayloadError>.Failure(
                    new PayloadError("Deserialization returned null"));
        }
        catch (Exception ex)
        {
            return Result<TOut, PayloadError>.Failure(
                new PayloadError(ex.Message) { Payload = Payload, Hash = Hash });
        }
    }

    #endregion

    #region Properties

    public ReadOnlyMemory<byte> Payload { get; }
    public string Hash { get; }
    public bool IsValid => IsSuccess && ValidateHash(Payload.Span, Hash);

    #endregion

    #region Public Factory Methods

    /// <summary>
    ///     Creates a new successful ResultWithPayload asynchronously
    /// </summary>
    public static async ValueTask<ResultWithPayload<T>> CreateAsync(
        T value,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var jsonStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(jsonStream, value, options, cancellationToken)
                .ConfigureAwait(false);

            var compressedData = await CompressDataAsync(
                jsonStream.ToArray(),
                cancellationToken).ConfigureAwait(false);

            var hash = ComputeHash(compressedData.Span);

            return new ResultWithPayload<T>(
                value,
                compressedData,
                hash,
                ResultState.Success);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return CreateFailureResult(ex.Message);
        }
    }

    /// <summary>
    ///     Creates a new successful ResultWithPayload synchronously
    /// </summary>
    public static ResultWithPayload<T> Create(
        T value,
        JsonSerializerOptions? options = null)
    {
        try
        {
            var jsonData = JsonSerializer.Serialize(value, options);
            var rawData = Encoding.UTF8.GetBytes(jsonData);
            var compressedData = CompressData(rawData);
            var hash = ComputeHash(compressedData.Span);

            return new ResultWithPayload<T>(
                value,
                compressedData,
                hash,
                ResultState.Success);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return CreateFailureResult(ex.Message);
        }
    }

    /// <summary>
    ///     Creates a failure result with the specified error message
    /// </summary>
    public static ResultWithPayload<T> Failure(string errorMessage)
    {
        return CreateFailureResult(errorMessage);
    }

    #endregion

    #region Private Helper Methods

    private static bool ValidateHash(ReadOnlySpan<byte> data, string expectedHash)
    {
        var actualHash = ComputeHash(data);
        return string.Equals(actualHash, expectedHash, StringComparison.Ordinal);
    }

    private static string ComputeHash(ReadOnlySpan<byte> data)
    {
        Span<byte> hashSpan = stackalloc byte[32]; // SHA256 = 32 bytes
        if (SHA256.TryHashData(data, hashSpan, out _))
        {
            return Convert.ToBase64String(hashSpan);
        }

        throw new InvalidOperationException("Hash computation failed");
    }

    private static async ValueTask<ReadOnlyMemory<byte>> CompressDataAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionLevel.Optimal))
        {
            await zip.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    private static ReadOnlyMemory<byte> CompressData(ReadOnlyMemory<byte> data)
    {
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionLevel.Optimal))
        {
            zip.Write(data.Span);
        }

        return output.ToArray();
    }

    private static async ValueTask<MemoryStream> DecompressToStreamAsync(
        ReadOnlyMemory<byte> compressedData,
        CancellationToken cancellationToken)
    {
        var outputStream = new MemoryStream();
        using var inputStream = new MemoryStream(compressedData.ToArray());
        using var gzip = new GZipStream(inputStream, CompressionMode.Decompress);

        await gzip.CopyToAsync(outputStream, BufferSize, cancellationToken)
            .ConfigureAwait(false);

        outputStream.Position = 0;
        return outputStream;
    }

    private static ResultWithPayload<T> CreateFailureResult(string errorMessage)
    {
        return new ResultWithPayload<T>(
            default,
            Array.Empty<byte>(),
            string.Empty,
            ResultState.Failure,
            new PayloadError(errorMessage));
    }

    #endregion
}
