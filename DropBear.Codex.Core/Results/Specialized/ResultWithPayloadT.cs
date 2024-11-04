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
///     Represents the outcome of an operation that includes a payload, with support for compression, hashing, and
///     validation.
/// </summary>
public class ResultWithPayload<T> : Result<T, PayloadError>
{
    private ResultWithPayload(
        T? value,
        byte[] payload,
        string hash,
        ResultState state,
        PayloadError? error = null,
        Exception? exception = null)
        : base(value!, state, error, exception)
    {
        Payload = payload;
        Hash = hash;
    }

    public byte[] Payload { get; }

    public string Hash { get; }

    public bool IsValid => IsSuccess && ValidateHash(Payload, Hash);

    public Result<TOut, PayloadError> DecompressAndDeserialize<TOut>()
    {
        if (!IsSuccess)
        {
            return Result<TOut, PayloadError>.Failure(
                new PayloadError("Operation failed, cannot decompress."));
        }

        try
        {
            using var input = new MemoryStream(Payload);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var decompressedJson = reader.ReadToEnd();

            if (!ValidateHash(Payload, Hash))
            {
                throw new InvalidOperationException("Data corruption detected during decompression.");
            }

            var deserializedData = JsonSerializer.Deserialize<TOut>(decompressedJson);
            return deserializedData is not null
                ? Result<TOut, PayloadError>.Success(deserializedData)
                : Result<TOut, PayloadError>.Failure(new PayloadError("Deserialization returned null."));
        }
        catch (Exception ex)
        {
            return Result<TOut, PayloadError>.Failure(
                new PayloadError(ex.Message) { Payload = Payload, Hash = Hash });
        }
    }

    private static bool ValidateHash(byte[] data, string expectedHash)
    {
        var actualHash = ComputeHash(data);
        return string.Equals(actualHash, expectedHash, StringComparison.Ordinal);
    }

    private static string ComputeHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToBase64String(hash);
    }

    private static async Task<byte[]> CompressAsync(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionMode.Compress))
        {
            await zip.WriteAsync(data).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionMode.Compress))
        {
            zip.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    public new static ResultWithPayload<T> Success(T data)
    {
        try
        {
            var jsonData = JsonSerializer.Serialize(data);
            var compressedData = Compress(Encoding.UTF8.GetBytes(jsonData));
            var hash = ComputeHash(compressedData);

            return new ResultWithPayload<T>(
                data,
                compressedData,
                hash,
                ResultState.Success);
        }
        catch (JsonException)
        {
            return new ResultWithPayload<T>(
                default,
                Array.Empty<byte>(),
                string.Empty,
                ResultState.Failure,
                new PayloadError("Serialization failed."));
        }
        catch (Exception ex)
        {
            return new ResultWithPayload<T>(
                default,
                Array.Empty<byte>(),
                string.Empty,
                ResultState.Failure,
                new PayloadError(ex.Message));
        }
    }

    public static ResultWithPayload<T> Failure(string error)
    {
        return new ResultWithPayload<T>(
            default,
            Array.Empty<byte>(),
            string.Empty,
            ResultState.Failure,
            new PayloadError(error));
    }

    public static async Task<ResultWithPayload<T>> SuccessAsync(T data)
    {
        try
        {
            var jsonData = JsonSerializer.Serialize(data);
            var compressedData = await CompressAsync(Encoding.UTF8.GetBytes(jsonData))
                .ConfigureAwait(false);
            var hash = ComputeHash(compressedData);

            return new ResultWithPayload<T>(
                data,
                compressedData,
                hash,
                ResultState.Success);
        }
        catch (JsonException)
        {
            return new ResultWithPayload<T>(
                default,
                Array.Empty<byte>(),
                string.Empty,
                ResultState.PartialSuccess,
                new PayloadError("Serialization partially failed."));
        }
        catch (Exception ex)
        {
            return new ResultWithPayload<T>(
                default,
                Array.Empty<byte>(),
                string.Empty,
                ResultState.Failure,
                new PayloadError(ex.Message));
        }
    }
}
