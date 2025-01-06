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
///     Represents a <see cref="Result{T,PayloadError}" /> containing a compressed and hashed payload.
///     Allows serialization/deserialization to/from JSON and verification of integrity via hashing.
/// </summary>
public sealed class ResultWithPayload<T> : Result<T, PayloadError>
{
    private const int BufferSize = 81920; // 80KB buffer, used for optimal compression/decompression

    #region Constructor

    /// <summary>
    ///     Initializes a new instance of the <see cref="ResultWithPayload{T}" /> class.
    /// </summary>
    /// <param name="value">The uncompressed value if the result is successful.</param>
    /// <param name="payload">A compressed representation of <paramref name="value" />.</param>
    /// <param name="hash">A SHA-256 hash of the <paramref name="payload" /> for integrity checks.</param>
    /// <param name="state">The <see cref="ResultState" /> (e.g., Success or Failure).</param>
    /// <param name="error">An optional <see cref="PayloadError" /> if this result is not successful.</param>
    /// <param name="exception">An optional exception that caused or contributed to failure.</param>
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
    ///     Decompresses and deserializes the payload to a new type <typeparamref name="TOut" />.
    ///     Verifies the hash before decompression. If invalid, returns a failure.
    /// </summary>
    /// <typeparam name="TOut">The type to deserialize to.</typeparam>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <param name="cancellationToken">A token to cancel the operation, if needed.</param>
    /// <returns>A <see cref="Result{TOut, PayloadError}" /> containing the deserialized object or an error.</returns>
    public async ValueTask<Result<TOut, PayloadError>> DecompressAndDeserializeAsync<TOut>(
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // If this result itself is not successful, there's no valid payload to decompress.
        if (!IsSuccess)
        {
            return Result<TOut, PayloadError>.Failure(
                new PayloadError("Cannot decompress a failed result"));
        }

        // If the hash is invalid, we can't trust the payload data.
        if (!IsValid)
        {
            return Result<TOut, PayloadError>.Failure(
                new PayloadError("Payload validation failed (hash mismatch)."));
        }

        try
        {
            // Decompress the stored payload into a stream
            using var decompressedStream = await DecompressToStreamAsync(
                Payload,
                cancellationToken).ConfigureAwait(false);

            // Deserialize the stream into the requested type
            var resultObject = await JsonSerializer.DeserializeAsync<TOut>(
                decompressedStream,
                options,
                cancellationToken).ConfigureAwait(false);

            return resultObject is not null
                ? Result<TOut, PayloadError>.Success(resultObject)
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

    /// <summary>
    ///     Gets the compressed payload data.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    ///     Gets the SHA-256 hash of the compressed payload, used for integrity checks.
    /// </summary>
    public string Hash { get; }

    /// <summary>
    ///     Checks if the current payload's hash is valid and if the result is a success.
    /// </summary>
    public bool IsValid => IsSuccess && ValidateHash(Payload.Span, Hash);

    #endregion

    #region Public Factory Methods

    /// <summary>
    ///     Asynchronously creates a new successful <see cref="ResultWithPayload{T}" /> by
    ///     serializing, compressing, and hashing the provided <paramref name="value" />.
    /// </summary>
    /// <param name="value">The object to serialize and store as a payload.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A success or failure <see cref="ResultWithPayload{T}" />.</returns>
    public static async ValueTask<ResultWithPayload<T>> CreateAsync(
        T value,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Serialize to JSON (in memory)
            using var jsonStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(jsonStream, value, options, cancellationToken)
                .ConfigureAwait(false);

            // Compress the JSON data
            var compressedData = await CompressDataAsync(
                jsonStream.ToArray(),
                cancellationToken).ConfigureAwait(false);

            // Compute a hash of the compressed payload
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
    ///     Creates a new successful <see cref="ResultWithPayload{T}" /> synchronously by
    ///     serializing, compressing, and hashing the provided <paramref name="value" />.
    /// </summary>
    /// <param name="value">The object to serialize and store as a payload.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>A success or failure <see cref="ResultWithPayload{T}" />.</returns>
    public static ResultWithPayload<T> Create(
        T value,
        JsonSerializerOptions? options = null)
    {
        try
        {
            // Serialize to JSON string
            var jsonData = JsonSerializer.Serialize(value, options);

            // Convert to bytes, then compress
            var rawData = Encoding.UTF8.GetBytes(jsonData);
            var compressedData = CompressData(rawData);

            // Compute hash
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
    ///     Creates a new <see cref="ResultWithPayload{T}" /> in the Failure state
    ///     with the specified <paramref name="errorMessage" />.
    /// </summary>
    public static ResultWithPayload<T> Failure(string errorMessage)
    {
        return CreateFailureResult(errorMessage);
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    ///     Validates whether the <paramref name="data" /> matches the <paramref name="expectedHash" />.
    /// </summary>
    private static bool ValidateHash(ReadOnlySpan<byte> data, string expectedHash)
    {
        var actualHash = ComputeHash(data);
        return string.Equals(actualHash, expectedHash, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Computes a Base64-encoded SHA-256 hash of the given <paramref name="data" />.
    /// </summary>
    private static string ComputeHash(ReadOnlySpan<byte> data)
    {
        Span<byte> hashSpan = stackalloc byte[32]; // 32 bytes for SHA256
        if (SHA256.TryHashData(data, hashSpan, out _))
        {
            return Convert.ToBase64String(hashSpan);
        }

        throw new InvalidOperationException("SHA-256 hash computation failed.");
    }

    /// <summary>
    ///     Compresses data asynchronously using GZip.
    /// </summary>
    private static async ValueTask<ReadOnlyMemory<byte>> CompressDataAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var zip = new GZipStream(output, CompressionLevel.Optimal);
        await using (zip.ConfigureAwait(false))
        {
            await zip.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Compresses data synchronously using GZip.
    /// </summary>
    private static ReadOnlyMemory<byte> CompressData(ReadOnlyMemory<byte> data)
    {
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionLevel.Optimal))
        {
            zip.Write(data.Span);
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Decompresses GZip-compressed data into a <see cref="MemoryStream" />.
    /// </summary>
    private static async ValueTask<MemoryStream> DecompressToStreamAsync(
        ReadOnlyMemory<byte> compressedData,
        CancellationToken cancellationToken)
    {
        var outputStream = new MemoryStream();

        // Convert ReadOnlyMemory<byte> -> byte[] -> input MemoryStream
        using var inputStream = new MemoryStream(compressedData.ToArray());
        var gzip = new GZipStream(inputStream, CompressionMode.Decompress);
        await using (gzip.ConfigureAwait(false))
        {
            await gzip.CopyToAsync(outputStream, BufferSize, cancellationToken)
            .ConfigureAwait(false);

        // Rewind the output stream to the beginning for the consumer
        outputStream.Position = 0;
        return outputStream;
        }
    }

    /// <summary>
    ///     Creates a <see cref="ResultWithPayload{T}" /> in the Failure state with a <see cref="PayloadError" />.
    /// </summary>
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
